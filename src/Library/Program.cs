using System.Globalization;
using System.Text;
using Library;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;
if (args.Length > 1)
{
    Console.Error.WriteLine("Usage: Library [data-file.json]");
    return 2;
}

try
{
    var path = args.Length == 1 ? args[0] : "data/library.sample.json";
    var loaded = JsonStore.Load(path);
    if (loaded is Result<LibraryState>.Ok(var state))
        Run(path, state);
    else
    {
        // Report the load error without opening an empty, writable session.
        PrintError(loaded);
        return 1;
    }
}
catch (EndOfStreamException)
{
    Console.WriteLine("Input ended.");
}
catch (IOException exception)
{
    Console.Error.WriteLine($"Console I/O error: {exception.Message}");
    return 1;
}
return 0;

static void Run(string path, LibraryState state)
{
    Console.WriteLine($"Library Lending | {path}");
    while (true)
    {
        Console.WriteLine("1 Register member   2 Acquire copies   3 Issue loan");
        Console.WriteLine("4 Renew loan        5 Return loan      6 Withdraw copies");
        Console.WriteLine("7 Overdue report    8 Demand report    9 List records   0 Exit");
        var command = Read("Command");
        try
        {
            switch (command)
            {
                case "0": return;
                case "1": Commit(Catalog.Register(state, Number("Member ID"), Read("Name"), Tier())); break;
                case "2": Commit(Catalog.Acquire(state, Number("Book ID"), Read("Title"),
                    Read("Category"), Number("Copies to add"))); break;
                case "3": Commit(Lending.Issue(state, Number("Book ID"), Number("Member ID"),
                    Number("New loan ID"), Date("Issue date"), Term())); break;
                case "4": Commit(Lending.Renew(state, Number("Loan ID"), Date("Renewal date"))); break;
                case "5": Commit(Lending.Return(state, Number("Loan ID"), Date("Return date"))); break;
                case "6": Commit(Catalog.Withdraw(state, Number("Book ID"), Number("Copies to withdraw"))); break;
                case "7":
                    var query = Reports.Overdue(state, Date("As-of date"), () => Console.WriteLine("Scanning loans now."));
                    Console.WriteLine("Overdue query prepared.");
                    var overdue = query.ToArray();
                    Console.WriteLine($"Overdue loans: {overdue.Length}");
                    foreach (var row in overdue)
                        Console.WriteLine($"{row.LoanId} | {row.Member} | {row.Title} | {row.DaysLate} days late");
                    break;
                case "8":
                    var from = Date("From date");
                    var through = Date("Through date");
                    if (from > through) throw new FormatException("From date must be on or before through date.");
                    var demand = Reports.Demand(state, from, through).ToArray();
                    Console.WriteLine($"Categories: {demand.Length}");
                    foreach (var row in demand)
                        Console.WriteLine($"{row.Category} | {row.Loans} loans | {row.Titles} titles");
                    break;
                case "9":
                    foreach (var book in state.Books)
                        Console.WriteLine($"Book {book.Id} | {book.Title} | {book.Category} | {book.Copies} copies");
                    foreach (var member in state.Members)
                        Console.WriteLine($"Member {member.Id} | {member.Name} | {member.Membership}");
                    foreach (var loan in state.Loans)
                        Console.WriteLine($"Loan {loan.Id} | book {loan.BookId} | member {loan.MemberId} | due {loan.DueOn:yyyy-MM-dd} | {(loan.ReturnedOn is null ? "active" : "returned")}");
                    break;
                default: Console.WriteLine("Choose a command from 0 to 9."); break;
            }
        }
        catch (FormatException exception)
        {
            Console.WriteLine($"Input error: {exception.Message}");
        }
    }

    void Commit(Result<Change> result)
    {
        if (result is Result<Change>.Ok(var change))
        {
            var saved = JsonStore.Save(path, change.State);
            if (saved is Result<string>.Ok)
            {
                state = change.State;
                Console.WriteLine(change.Message);
            }
            else PrintError(saved);
        }
        else PrintError(result);
    }
}

static void PrintError<T>(Result<T> result)
{
    if (result is Result<T>.Error(var code, var message)) Console.WriteLine($"Error [{code}] {message}");
}

static string Read(string label)
{
    Console.Write($"{label}: ");
    return Console.ReadLine()?.Trim() ?? throw new EndOfStreamException();
}

static int Number(string label) => int.TryParse(Read(label), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
    ? number : throw new FormatException($"{label} must be an integer.");

static DateOnly Date(string label) =>
    DateOnly.TryParseExact(Read($"{label} (yyyy-MM-dd)"), "yyyy-MM-dd", CultureInfo.InvariantCulture,
        DateTimeStyles.None, out var date) ? date : throw new FormatException("Use yyyy-MM-dd.");

static Membership Tier() => Read("Membership (1 Standard, 2 Extended)") switch
{
    "1" => Membership.Standard,
    "2" => Membership.Extended,
    _ => throw new FormatException("Choose membership 1 or 2.")
};

static Func<Member, int> Term() => Read("Loan policy (1 regular, 2 short)") switch
{
    "1" => Lending.StandardTerm,
    "2" => Lending.ShortTerm,
    _ => throw new FormatException("Choose policy 1 or 2.")
};
