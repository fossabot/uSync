﻿using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using Microsoft.Extensions.Logging;

using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Extensions;

using uSync.BackOffice.Configuration;
using uSync.BackOffice.Services;
using uSync.Core;

using static Umbraco.Cms.Core.Constants;

namespace uSync.BackOffice.SyncHandlers.Handlers;

/// <summary>
///  Handler to mange Relation types in uSync
/// </summary>
[SyncHandler(uSyncConstants.Handlers.RelationTypeHandler, "Relations",
        "RelationTypes", uSyncConstants.Priorites.RelationTypes,
        Icon = "icon-link",
        EntityType = UdiEntityType.RelationType, IsTwoPass = false)]
public class RelationTypeHandler : SyncHandlerBase<IRelationType, IRelationService>, ISyncHandler,
    INotificationHandler<SavedNotification<IRelationType>>,
    INotificationHandler<DeletedNotification<IRelationType>>,
    INotificationHandler<SavingNotification<IRelationType>>,
    INotificationHandler<DeletingNotification<IRelationType>>
{
    private readonly IRelationService relationService;

    /// <inheritdoc/>
    public override string Group => uSyncConstants.Groups.Content;

    /// <inheritdoc/>
    public RelationTypeHandler(
        ILogger<RelationTypeHandler> logger,
        IEntityService entityService,
        IRelationService relationService,
        AppCaches appCaches,
        IShortStringHelper shortStringHelper,
        SyncFileService syncFileService,
        uSyncEventService mutexService,
        uSyncConfigService uSyncConfigService,
        ISyncItemFactory syncItemFactory)
        : base(logger, entityService, appCaches, shortStringHelper, syncFileService, mutexService, uSyncConfigService, syncItemFactory)
    {
        this.relationService = relationService;
    }

    /// <inheritdoc/>
    public override IEnumerable<uSyncAction> ExportAll(string folder, HandlerSettings config, SyncUpdateCallback? callback)
    {
        var actions = new List<uSyncAction>();

        var items = relationService.GetAllRelationTypes().ToList();

        foreach (var item in items.Select((relationType, index) => new { relationType, index }))
        {
            callback?.Invoke(item.relationType.Name ?? item.relationType.Alias, item.index, items.Count);
            actions.AddRange(Export(item.relationType, folder, config));
        }

        return actions;
    }

    /// <summary>
    ///  Relations that by default we exclude, if the exlude setting is used,then it will override these values
    ///  and they will be included if not explicity set;
    /// </summary>
    private const string defaultRelations = "relateParentDocumentOnDelete,relateParentMediaFolderOnDelete,relateDocumentOnCopy,umbMedia,umbDocument";

    /// <summary>
    ///  Workout if we are excluding this relationType from export/import
    /// </summary>
    protected override bool ShouldExport(XElement node, HandlerSettings config)
    {
        var exclude = config.GetSetting<string>("Exclude", defaultRelations);

        if (!string.IsNullOrWhiteSpace(exclude) && exclude.Contains(node.GetAlias()))
            return false;

        return true;
    }

    /// <inheritdoc/>
    protected override bool ShouldImport(XElement node, HandlerSettings config)
        => ShouldExport(node, config);


    /// <inheritdoc/>
    protected override string GetItemName(IRelationType item)
        => item.Name ?? item.Alias;

    /// <inheritdoc/>
    protected override string GetItemFileName(IRelationType item)
        => GetItemAlias(item).ToSafeAlias(shortStringHelper);

    /// <inheritdoc/>
    protected override IEnumerable<IEntity> GetChildItems(int parent)
    {
        if (parent == -1)
            return relationService.GetAllRelationTypes();

        return [];
    }

}
