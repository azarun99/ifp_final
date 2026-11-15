namespace Library;

public static class Lending
{
    public static int StandardTerm(Member member) => member.Membership switch
    {
        Membership.Extended => 21,
        _ => 14
    };

    public static int ShortTerm(Member member) => member.Membership switch
    {
        Membership.Extended => 10,
        _ => 7
    };

    private sealed record IssueContext(LibraryState State, Book Book, Member Member, int LoanId, DateOnly Date);
    private sealed record ScheduledIssue(IssueContext Request, DateOnly Due);

    public static Result<Change> Issue(LibraryState state, int bookId, int memberId, int loanId,
        DateOnly date, Func<Member, int> term) =>
        ResolveIssue(state, bookId, memberId, loanId, date)
            .Bind(CheckEligibility)
            .Bind(request => ScheduleIssue(request, term))
            .Map(RecordIssue);

    private static Result<(Book Book, Member Member)> ResolveParties(LibraryState state, int bookId, int memberId) =>
        state.Books.Find(book => book.Id == bookId).Require("Book does not exist.")
            .Bind(book => state.Members.Find(member => member.Id == memberId).Require("Member does not exist.")
                .Map(member => (book, member)));

    private static Result<IssueContext> ResolveIssue(LibraryState state, int bookId, int memberId,
        int loanId, DateOnly date) =>
        ResolveParties(state, bookId, memberId)
            .Map(parties => new IssueContext(state, parties.Book, parties.Member, loanId, date));

    private static Result<IssueContext> CheckEligibility(IssueContext request)
    {
        if (request.LoanId <= 0 || request.State.Loans.Any(loan => loan.Id == request.LoanId))
            return new Result<IssueContext>.Error("invalid_loan_id", "Loan ID must be positive and unused.");

        return CheckLendingRules(request.State, request.Book, request.Member, request.Date).Map(_ => request);
    }

    private static Result<Member> CheckLendingRules(LibraryState state, Book book, Member member, DateOnly date)
    {
        var active = state.Loans.Where(loan => loan.ReturnedOn is null).ToArray();
        var memberLoans = active.Where(loan => loan.MemberId == member.Id).ToArray();
        if (memberLoans.Any(loan => loan.BookId == book.Id))
            return new Result<Member>.Error("duplicate_loan", "Member already has this title.");
        if (memberLoans.Any(loan => loan.IsOverdue(date)))
            return new Result<Member>.Error("overdue", "Member has overdue loans.");
        if (memberLoans.Length >= 3)
            return new Result<Member>.Error("loan_limit", "Member already has three active loans.");
        if (active.Count(loan => loan.BookId == book.Id) >= book.Copies)
            return new Result<Member>.Error("unavailable", "All copies are on loan.");

        return new Result<Member>.Ok(member);
    }

    private static Result<ScheduledIssue> ScheduleIssue(IssueContext request, Func<Member, int> term) =>
        AddDays(request.Date, term(request.Member)).Map(due => new ScheduledIssue(request, due));

    private static Change RecordIssue(ScheduledIssue scheduled)
    {
        var request = scheduled.Request;
        var loan = new Loan(request.LoanId, request.Book.Id, request.Member.Id, request.Date, scheduled.Due);
        var state = request.State;
        return new Change(new LibraryState(state.Books, state.Members, state.Loans.Add(loan)),
            FormattableString.Invariant($"Issued loan {loan.Id}. Due {loan.DueOn:yyyy-MM-dd}."));
    }

    public static Result<Change> Renew(LibraryState state, int loanId, DateOnly date) =>
        ActiveLoan(state, loanId, date).Bind(loan => loan switch
        {
            { Renewals: >= 1 } => new Result<Change>.Error("renewal_limit", "Loan was already renewed."),
            _ when loan.IsOverdue(date) => new Result<Change>.Error("overdue", "Overdue loans cannot be renewed."),
            _ => AddDays(loan.DueOn, 7).Map(due => ReplaceLoan(state, loan,
                loan with { DueOn = due, Renewals = 1 }, FormattableString.Invariant($"Renewed loan {loan.Id}. Due {due:yyyy-MM-dd}.")))
        });

    public static Result<Change> Return(LibraryState state, int loanId, DateOnly date) =>
        ActiveLoan(state, loanId, date).Map(loan =>
        {
            var lateDays = Math.Max(0, date.DayNumber - loan.DueOn.DayNumber);
            return ReplaceLoan(state, loan, loan with { ReturnedOn = date },
                $"Returned loan {loan.Id}. Days late {lateDays}.");
        });

    private static Result<Loan> ActiveLoan(LibraryState state, int loanId, DateOnly date) =>
        state.Loans.Find(loan => loan.Id == loanId).Require("Issue the loan before changing it.")
            .Bind<Loan>(loan => loan switch
            {
                { ReturnedOn: not null } => new Result<Loan>.Error("closed_loan", "Loan is already closed."),
                _ when date < loan.IssuedOn => new Result<Loan>.Error("invalid_date", "Date precedes issue date."),
                _ => new Result<Loan>.Ok(loan)
            });

    private static Result<DateOnly> AddDays(DateOnly date, int days) =>
        days <= 0 || days > DateOnly.MaxValue.DayNumber - date.DayNumber
            ? new Result<DateOnly>.Error("invalid_term", "Term must be positive and fit the calendar.")
            : new Result<DateOnly>.Ok(date.AddDays(days));

    private static Change ReplaceLoan(LibraryState state, Loan oldLoan, Loan newLoan, string message) =>
        new(new LibraryState(state.Books, state.Members, state.Loans.Replace(oldLoan, newLoan)), message);
}
