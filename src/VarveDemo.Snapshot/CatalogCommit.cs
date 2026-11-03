// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;

namespace VarveDemo.Snapshot;

/// <summary>One leaf of a catalog commit, as its catalog page lists it.</summary>
/// <param name="Url">The leaf's URL, its <c>@id</c>.</param>
/// <param name="IsDelete">A <c>PackageDelete</c> leaf; otherwise <c>PackageDetails</c>.</param>
/// <param name="Id">The package id, as the page gives it.</param>
/// <param name="Version">The package version, normalised, as the page gives it.</param>
public sealed record CatalogLeafRef(string Url, bool IsDelete, string Id, string Version)
{
    /// <summary>The order of leaves inside a commit: by id and version, case-insensitively, then type and URL.</summary>
    public static int Compare(CatalogLeafRef? x, CatalogLeafRef? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null || y is null)
        {
            return x is null ? -1 : 1;
        }

        int order = string.Compare(x.Id, y.Id, StringComparison.OrdinalIgnoreCase);
        order = order != 0 ? order : string.Compare(x.Version, y.Version, StringComparison.OrdinalIgnoreCase);
        order = order != 0 ? order : x.IsDelete.CompareTo(y.IsDelete);
        return order != 0 ? order : string.CompareOrdinal(x.Url, y.Url);
    }
}

/// <summary>One retained catalog commit: its id, its timestamp as the catalog writes it, and its leaves in the subset.</summary>
/// <param name="CommitId">The catalog's <c>commitId</c>.</param>
/// <param name="CommitTimeStamp">The catalog's <c>commitTimeStamp</c>, exactly as the page writes it.</param>
/// <param name="Leaves">The commit's leaves for packages in the subset, in <see cref="CatalogLeafRef.Compare"/> order.</param>
public sealed record CatalogCommit(string CommitId, string CommitTimeStamp, IReadOnlyList<CatalogLeafRef> Leaves);
