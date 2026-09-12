using System.Reflection;
using RoomBook.Api.Problems;
using RoomBook.Domain.Shared;

namespace RoomBook.Api.Tests.Problems;

public sealed class ErrorResponsesTests
{
    [Fact]
    public void EveryErrorCode_HasAnHttpStatus()
    {
        // A code with no status throws at runtime, which would turn a documented refusal into a 500.
        // This test is the reason that cannot happen: adding a code without a status fails here.
        IReadOnlyList<string> codes = typeof(ErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true, FieldType: { } type } && type == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        Assert.NotEmpty(codes);

        IReadOnlyList<string> unmapped = codes
            .Where(code => !ErrorResponses.StatusByCode.ContainsKey(code))
            .ToList();

        Assert.Empty(unmapped);
    }

    [Fact]
    public void StatusByCode_MatchesTheDocumentedTable()
    {
        // docs/conventions.md: 400 malformed, 404 unknown identifier, 409 state conflict,
        // 422 business-rule violation.
        Assert.Equal(400, ErrorResponses.StatusByCode[ErrorCodes.RequestInvalid]);
        Assert.Equal(413, ErrorResponses.StatusByCode[ErrorCodes.RequestTooLarge]);
        Assert.Equal(404, ErrorResponses.StatusByCode[ErrorCodes.RoomNotFound]);
        Assert.Equal(404, ErrorResponses.StatusByCode[ErrorCodes.BookingNotFound]);
        Assert.Equal(409, ErrorResponses.StatusByCode[ErrorCodes.BookingOverlap]);
        Assert.Equal(422, ErrorResponses.StatusByCode[ErrorCodes.OutsideBusinessHours]);
        Assert.Equal(422, ErrorResponses.StatusByCode[ErrorCodes.DurationOutOfRange]);
        Assert.Equal(422, ErrorResponses.StatusByCode[ErrorCodes.AttendeesExceedCapacity]);
        Assert.Equal(422, ErrorResponses.StatusByCode[ErrorCodes.TooFarInFuture]);
        Assert.Equal(422, ErrorResponses.StatusByCode[ErrorCodes.StartInPast]);
    }
}
