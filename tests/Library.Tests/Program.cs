using Library;

var date = new DateOnly(2026, 11, 13);
Book book = new(10, "Orbit", "Science", 2);
Member member = new(1, "Amina", Membership.Standard);
var cases = new (string Name, Action Run)[]
{
    ("Pure | Register trims name", () =>
        Equal("Ada", Ok(Catalog.Register(LibraryState.Empty, 1, " Ada ", Membership.Standard)).State.Members[0].Name)),
    ("Pure | Invalid member ID", () =>
        Error("invalid_member", Catalog.Register(LibraryState.Empty, 0, "Ada", Membership.Standard))),
    ("Pure | Conflicting catalog metadata", () =>
        Error("catalog_conflict", Catalog.Acquire(State(), 10, "Other", "Science", 1))),
    ("Pure | Withdrawal preserves one copy", () =>
    {
        Equal(1, Ok(Catalog.Withdraw(State(), 10, 1)).State.Books[0].Copies);
        Error("withdrawal_limit", Catalog.Withdraw(State(), 10, 2));
    }),
    ("Pure | Withdrawal preserves active copies", () =>
    {
        var state = Ok(LibraryState.Create([book], [member, new Member(2, "Ada", Membership.Extended)],
            [new Loan(7, 10, 1, date, date.AddDays(7)), new Loan(8, 10, 2, date, date.AddDays(7))]));
        Error("withdrawal_limit", Catalog.Withdraw(state, 10, 1));
        Equal(2, state.Books[0].Copies);
    }),
    ("Pure | Missing lookup", () =>
        Error("not_found", Lending.Issue(State(), 99, 1, 7, date, Lending.StandardTerm))),
    ("Pure | Overdue boundary", () =>
    {
        Loan loan = new(7, 10, 1, date.AddDays(-7), date);
        Equal(false, loan.IsOverdue(date));
        Equal(true, loan.IsOverdue(date.AddDays(1)));
    }),
    ("Pure | Empty reports", () =>
    {
        Equal(0, Reports.Overdue(LibraryState.Empty, date).Count());
        Equal(0, Reports.Demand(LibraryState.Empty, date, date).Count());
    }),
    ("Pure | Invalid references", () =>
        Error("invalid_data", LibraryState.Create([book], [member], [new Loan(7, 99, 1, date, date.AddDays(7))]))),
    ("Pure | Snapshot detaches from caller list", () =>
    {
        List<Book> books = [book];
        var state = Ok(LibraryState.Create(books, [member], []));
        books.Clear();
        Equal(1, state.Books.Length);
    }),
    ("Pure | Lazy execution and stable snapshot", () =>
    {
        var scans = 0;
        var state = State(new Loan(7, 10, 1, date.AddDays(-10), date.AddDays(-1)));
        var query = Reports.Overdue(state, date, () => scans++);
        Equal(0, scans);
        var rows = query.ToArray();
        Equal(1, rows.Single().DaysLate);
        Equal(1, scans);
        Equal(7, rows.Single().LoanId);
        Equal(1, scans);
    }),
    ("Pure | Demand includes both endpoints and orders ties", () =>
    {
        var state = Ok(LibraryState.Create([book, new Book(11, "Code", "Computing", 1)], [member],
            [new Loan(7, 10, 1, date, date.AddDays(7)), new Loan(8, 11, 1, date, date.AddDays(7))]));
        var rows = Reports.Demand(state, date, date).ToArray();
        Equal(2, rows.Length);
        Equal(new DemandRow("Computing", 1, 1), rows[0]);
    }),
    ("Workflow | Issue with two policies, renew and return", () =>
    {
        var start = State();
        var regular = Ok(Lending.Issue(start, 10, 1, 7, date, Lending.StandardTerm)).State;
        var shortTerm = Ok(Lending.Issue(start, 10, 1, 7, date, Lending.ShortTerm)).State;
        Equal(date.AddDays(14), regular.Loans.Single().DueOn);
        Equal(date.AddDays(7), shortTerm.Loans.Single().DueOn);
        var renewed = Ok(Lending.Renew(shortTerm, 7, date.AddDays(7))).State;
        Equal(date.AddDays(14), renewed.Loans.Single().DueOn);
        Error("renewal_limit", Lending.Renew(renewed, 7, date.AddDays(8)));
        var returned = Ok(Lending.Return(renewed, 7, date.AddDays(16)));
        Equal("Returned loan 7. Days late 2.", returned.Message);
        Error("closed_loan", Lending.Return(returned.State, 7, date.AddDays(16)));
        Equal(0, start.Loans.Length);
    }),
    ("Workflow | Invalid order and failure skip later stages", () =>
    {
        var start = State();
        Error("not_found", Lending.Return(start, 7, date));
        var active = State(new Loan(6, 10, 1, date.AddDays(-8), date.AddDays(-1)));
        var invoked = false;
        Error("duplicate_loan", Lending.Issue(active, 10, 1, 7, date, _ => { invoked = true; return 7; }));
        Equal(false, invoked);
        Equal(1, active.Loans.Length);
        Error("invalid_term", Lending.Issue(start, 10, 1, 7, DateOnly.MaxValue, Lending.StandardTerm));
    }),
    ("File | Missing input", () => InTemp(path => Error("missing_file", JsonStore.Load(path)))),
    ("File | Malformed JSON", () => InTemp(path =>
    {
        File.WriteAllText(path, "{broken");
        Error("malformed_json", JsonStore.Load(path));
    })),
    ("File | Save and reload preserves loan", () => InTemp(path =>
    {
        var state = Ok(Lending.Issue(State(), 10, 1, 7, date, Lending.ShortTerm)).State;
        Ok(JsonStore.Save(path, state));
        Equal(state.Loans.Single(), Ok(JsonStore.Load(path)).Loans.Single());
    }))
};

var passed = 0;
foreach (var (name, run) in cases)
{
    try
    {
        run();
        passed++;
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception error)
    {
        Console.WriteLine($"FAIL {name}: {error.Message}");
    }
}
Console.WriteLine($"{passed}/{cases.Length} passed.");
return passed == cases.Length ? 0 : 1;

LibraryState State(params Loan[] loans) => Ok(LibraryState.Create([book], [member], loans));

static T Ok<T>(Result<T> result) => result is Result<T>.Ok(var value)
    ? value : throw new InvalidOperationException($"Expected success, received {result}.");

static void Error<T>(string expected, Result<T> result)
{
    if (result is not Result<T>.Error(var actual, _))
        throw new InvalidOperationException($"Expected {expected}, received {result}.");
    Equal(expected, actual);
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"Expected {expected}, received {actual}.");
}

static void InTemp(Action<string> run)
{
    var directory = Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), $".library-tests-{Guid.NewGuid():N}"));
    try { run(Path.Combine(directory.FullName, "library.json")); }
    finally { directory.Delete(recursive: true); }
}
