// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

namespace LitheView.Designer.Core.Schema;

public static class WidgetCatalogMigrator
{
    public static string ResolveWidgetId(
        WidgetCatalog catalog,
        string sourceId,
        int sourceCompatibilityVersion)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);

        string current = sourceId;
        for (int version = sourceCompatibilityVersion; version < catalog.CompatibilityVersion; ++version)
        {
            WidgetMigration? migration = catalog.Migrations.SingleOrDefault(item =>
                item.Kind == WidgetMigrationKind.RenameWidget &&
                item.FromCompatibilityVersion == version &&
                item.FromId == current);
            if (migration is not null)
            {
                current = migration.ToId;
            }
        }
        return current;
    }

    public static string ResolvePropertyId(
        WidgetCatalog catalog,
        string? widgetId,
        string sourceId,
        int sourceCompatibilityVersion)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);

        string current = sourceId;
        for (int version = sourceCompatibilityVersion; version < catalog.CompatibilityVersion; ++version)
        {
            WidgetMigration? migration = catalog.Migrations
                .Where(item =>
                    item.Kind == WidgetMigrationKind.RenameProperty &&
                    item.FromCompatibilityVersion == version &&
                    (item.WidgetId == widgetId || item.WidgetId is null) &&
                    item.FromId == current)
                .OrderByDescending(item => item.WidgetId is not null)
                .FirstOrDefault();
            if (migration is not null)
            {
                current = migration.ToId;
            }
        }
        return current;
    }
}
