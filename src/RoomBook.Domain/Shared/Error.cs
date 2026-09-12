namespace RoomBook.Domain.Shared;

/// <summary>
/// A rule violation carried as a value: a stable machine-readable code plus a readable message.
/// The code is part of the public contract — renaming one is a breaking change (ADR-0003).
/// </summary>
public sealed record Error(string Code, string Message);
