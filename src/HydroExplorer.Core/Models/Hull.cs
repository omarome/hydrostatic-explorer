namespace HydroExplorer.Core.Models;

/// <summary>
/// A hull: a box barge, or a ship-shaped hull when <see cref="Shape"/> is set. All dimensions in metres.
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
/// <param name="Shape">
/// Null for a box barge (exact formulas). Otherwise the ship's hull form, whose hydrostatics are found by
/// numerical integration. Optional and last, so existing code and JSON without "shape" keep working.
/// </param>
public sealed record Hull(int Id, string Name, double Length, double Beam, double Depth, HullShape? Shape = null);
