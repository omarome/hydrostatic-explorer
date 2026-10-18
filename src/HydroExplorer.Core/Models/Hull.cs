namespace HydroExplorer.Core.Models;

/// <summary>
/// A box-shaped hull (box barge). All dimensions in metres.
/// </summary>
/// <remarks>
/// A "positional record" is an immutable class with value equality, built from the constructor
/// parameters. Similar to a readonly TypeScript type plus a structural equals().
/// </remarks>
/// <param name="Id">Unique id, used in API routes.</param>
/// <param name="Name">Display name.</param>
/// <param name="Length">Length L (m).</param>
/// <param name="Beam">Beam (breadth) B (m).</param>
/// <param name="Depth">Depth D, keel to deck (m). The maximum draft.</param>
public sealed record Hull(int Id, string Name, double Length, double Beam, double Depth);
