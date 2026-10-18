using HydroExplorer.Core.Models;

namespace HydroExplorer.Core.Data;

/// <summary>
/// Read access to hulls. An interface so the API depends on "something that provides hulls",
/// not on the seed data (it could later be a database).
/// </summary>
public interface IHullRepository
{
    /// <summary>All hulls, ordered by id.</summary>
    IReadOnlyList<Hull> GetAll();

    /// <summary>The hull with this id, or null if there is none.</summary>
    Hull? GetById(int id);
}
