namespace Library;

public static class Catalog
{
    public static Result<Change> Register(LibraryState state, int id, string name, Membership membership)
    {
        if (id <= 0 || string.IsNullOrWhiteSpace(name) || !Enum.IsDefined(membership))
            return new Result<Change>.Error("invalid_member", "Enter a positive ID, a name and a valid membership.");
        if (state.Members.Find(member => member.Id == id) is Option<Member>.Some)
            return new Result<Change>.Error("duplicate_member", "Member ID already exists.");

        var members = state.Members.Add(new Member(id, name.Trim(), membership));
        return new Result<Change>.Ok(new Change(new LibraryState(state.Books, members, state.Loans),
            $"Registered member {id}."));
    }

    public static Result<Change> Acquire(LibraryState state, int id, string title, string category, int copies)
    {
        if (id <= 0 || copies <= 0)
            return new Result<Change>.Error("invalid_stock", "Enter a positive book ID and copy count.");

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(category))
            return new Result<Change>.Error("invalid_book", "A book needs a title and category.");
        if (state.Books.Find(book => book.Id == id) is Option<Book>.Some(var existing))
        {
            if (!string.Equals(existing.Title, title.Trim(), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing.Category, category.Trim(), StringComparison.OrdinalIgnoreCase))
                return new Result<Change>.Error("catalog_conflict", "That ID belongs to a different title or category.");
            if (copies > int.MaxValue - existing.Copies)
                return new Result<Change>.Error("invalid_stock", "Total copies exceed the supported limit.");
            var updated = state.Books.Replace(existing, existing with { Copies = existing.Copies + copies });
            return new Result<Change>.Ok(new Change(new LibraryState(updated, state.Members, state.Loans),
                $"Added {copies} copies to book {id}."));
        }

        var books = state.Books.Add(new Book(id, title.Trim(), category.Trim(), copies));
        return new Result<Change>.Ok(new Change(new LibraryState(books, state.Members, state.Loans),
            $"Added book {id} with {copies} copies."));
    }

    public static Result<Change> Withdraw(LibraryState state, int id, int copies) =>
        state.Books.Find(book => book.Id == id).Require("Book does not exist.").Bind<Change>(book =>
        {
            var onLoan = state.Loans.Count(loan => loan.BookId == id && loan.ReturnedOn is null);
            var removable = book.Copies - Math.Max(1, onLoan);
            if (copies <= 0 || copies > removable)
                return new Result<Change>.Error("withdrawal_limit",
                    $"Remove between 1 and {removable} copies, subject to active loans and one retained copy.");

            var books = state.Books.Replace(book, book with { Copies = book.Copies - copies });
            return new Result<Change>.Ok(new Change(new LibraryState(books, state.Members, state.Loans),
                $"Withdrew {copies} copies of book {id}."));
        });
}
