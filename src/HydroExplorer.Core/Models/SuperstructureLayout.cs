using System.Text.Json.Serialization;

namespace HydroExplorer.Core.Models;

/// <summary>
/// Where a ship's superstructure stands. Decorative: it changes the 3D and side drawings, never the hydrostatics.
/// </summary>
/// <remarks>
/// The attribute makes the JSON say "ForwardAccommodation" instead of the number 1, for the API and for the
/// app reading it. System.Text.Json is part of .NET, so Core still has no package dependencies.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SuperstructureLayout
{
    /// <summary>Deckhouse, bridge and funnel aft, cargo forward: tankers, container feeders, coasters.</summary>
    AftAccommodation,

    /// <summary>
    /// Accommodation, bridge and helideck forward, walk-to-work gangway amidships, open working deck aft:
    /// a Service Operation Vessel (SOV) for offshore wind farms.
    /// </summary>
    ForwardAccommodation,
}
