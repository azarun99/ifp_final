using System.Collections.Immutable;

namespace Library;

public enum Membership { Standard, Extended }
public sealed record Book(int Id, string Title, string Category, int Copies);
public sealed record Member(int Id, string Name, Membership Membership);
public sealed record Loan(int Id, int BookId, int MemberId, DateOnly IssuedOn, DateOnly DueOn,
    int Renewals = 0, DateOnly? ReturnedOn = null);
public sealed record Change(LibraryState State, string Message);

public sealed class LibraryState
{
    public ImmutableArray<Book> Books { get; }
    public ImmutableArray<Member> Members { get; }
    public ImmutableArray<Loan> Loans { get; }

    internal LibraryState(ImmutableArray<Book> books, ImmutableArray<Member> members, ImmutableArray<Loan> loans)
    {
        Books = books;
        Members = members;
        Loans = loans;
    }

    public static LibraryState Empty { get; } = new([], [], []);

    public static Result<LibraryState> Create(IEnumerable<Book> books, IEnumerable<Member> members,
        IEnumerable<Loan> loans)
    {
        var state = new LibraryState([.. books], [.. members], [.. loans]);
        if (state.Books.Any(book => book is null || book.Id <= 0 || book.Copies <= 0 ||
                string.IsNullOrWhiteSpace(book.Title) || string.IsNullOrWhiteSpace(book.Category)) ||
            state.Members.Any(member => member is null || member.Id <= 0 ||
                string.IsNullOrWhiteSpace(member.Name) || !Enum.IsDefined(member.Membership)) ||
            state.Loans.Any(loan => loan is null || loan.Id <= 0 || loan.Renewals is < 0 or > 1 ||
                loan.DueOn <= loan.IssuedOn || loan.ReturnedOn < loan.IssuedOn))
            return Invalid("A record has invalid fields.");

        var bookIds = state.Books.Select(book => book.Id).ToHashSet();
        var memberIds = state.Members.Select(member => member.Id).ToHashSet();
        if (bookIds.Count != state.Books.Length || memberIds.Count != state.Members.Length ||
            state.Loans.Select(loan => loan.Id).Distinct().Count() != state.Loans.Length)
            return Invalid("Record IDs must be unique within their type.");

        if (state.Loans.Any(loan => !bookIds.Contains(loan.BookId) || !memberIds.Contains(loan.MemberId)))
            return Invalid("A loan refers to an unknown book or member.");

        var active = state.Loans.Where(loan => loan.ReturnedOn is null).ToArray();
        var capacity = state.Books.ToDictionary(book => book.Id, book => book.Copies);
        if (active.GroupBy(loan => loan.BookId).Any(group => group.Count() > capacity[group.Key]) ||
            active.GroupBy(loan => (loan.MemberId, loan.BookId)).Any(group => group.Count() > 1))
            return Invalid("Active loans exceed stock or duplicate a member's loan.");

        return new Result<LibraryState>.Ok(state);
    }

    private static Result<LibraryState> Invalid(string message) =>
        new Result<LibraryState>.Error("invalid_data", message);
}
