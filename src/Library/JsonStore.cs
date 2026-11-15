using System.Text.Json;
using System.Text.Json.Serialization;

namespace Library;

public static class JsonStore
{
    private sealed record Data(Book[]? Books, Member[]? Members, Loan[]? Loans);
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public static Result<LibraryState> Load(string path)
    {
        try
        {
            var data = JsonSerializer.Deserialize<Data>(File.ReadAllText(path), Options);
            if (data?.Books is null || data.Members is null || data.Loans is null)
                return new Result<LibraryState>.Error("invalid_data", "JSON must contain books, members and loans arrays.");
            return LibraryState.Create(data.Books, data.Members, data.Loans);
        }
        catch (JsonException exception)
        {
            return new Result<LibraryState>.Error("malformed_json", $"Invalid JSON at {exception.Path ?? "root"}.");
        }
        catch (IOException exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return new Result<LibraryState>.Error("missing_file", $"File not found: {path}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new Result<LibraryState>.Error("read_error", exception.Message);
        }
    }

    public static Result<string> Save(string path, LibraryState state)
    {
        string? temporary = null;
        try
        {
            var destination = Path.GetFullPath(path);
            temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".library-{Guid.NewGuid():N}.tmp");
            var data = new Data([.. state.Books], [.. state.Members], [.. state.Loans]);
            File.WriteAllText(temporary, JsonSerializer.Serialize(data, Options));
            File.Move(temporary, destination, overwrite: true);
            return new Result<string>.Ok(destination);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new Result<string>.Error("write_error", exception.Message);
        }
        finally
        {
            if (temporary is not null)
            {
                try { File.Delete(temporary); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
            }
        }
    }
}
