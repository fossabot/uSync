﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using Microsoft.Extensions.Logging;

using Umbraco.Extensions;

using uSync.BackOffice.Extensions;
using uSync.BackOffice.Models;
using uSync.BackOffice.SyncHandlers;
using uSync.BackOffice.SyncHandlers.Interfaces;
using uSync.Core;

using static Umbraco.Cms.Core.Constants;

namespace uSync.BackOffice;


/// <summary>
/// Implementation of paged import methods.
/// </summary>
public partial class uSyncService
{
    /// <summary>
    ///  Perform a paged report against a given folder 
    /// </summary>
    [Obsolete("For better performance pass handler, will be removed in v15")]
    public IEnumerable<uSyncAction> ReportPartial(string folder, uSyncPagedImportOptions options, out int total)
        => ReportPartial([folder], options, out total);

    /// <summary>
    ///  Perform a paged report against a given folder 
    /// </summary>
    [Obsolete("For better performance pass handler, will be removed in v15")]
    public IEnumerable<uSyncAction> ReportPartial(string[] folder, uSyncPagedImportOptions options, out int total)
    {
        var orderedNodes = LoadOrderedNodes(folder);
        return ReportPartial(orderedNodes, options, out total);
    }

    /// <summary>
    ///  perform a paged report with the supplied ordered nodes
    /// </summary>
    public IEnumerable<uSyncAction> ReportPartial(IList<OrderedNodeInfo> orderedNodes, uSyncPagedImportOptions options, out int total)
    {
        total = orderedNodes.Count;

        var actions = new List<uSyncAction>();
        var lastType = string.Empty;

        var folder = Path.GetDirectoryName(orderedNodes.FirstOrDefault()?.FileName ?? options.Folders?.FirstOrDefault() ?? _uSyncConfig.GetRootFolder()) ?? string.Empty;

        SyncHandlerOptions syncHandlerOptions = HandlerOptionsFromPaged(options);

        HandlerConfigPair? handlerPair = null;

        var index = options.PageNumber * options.PageSize;

        foreach (var item in orderedNodes.Skip(options.PageNumber * options.PageSize).Take(options.PageSize))
        {
            var itemType = item.Node.GetItemType();
            if (!itemType.InvariantEquals(lastType))
            {
                lastType = itemType;
                handlerPair = _handlerFactory.GetValidHandlerByTypeName(itemType, syncHandlerOptions);

                handlerPair?.Handler.PreCacheFolderKeys(folder, orderedNodes.Select(x => x.Key).ToList());
            }

            if (handlerPair == null)
            {
                _logger.LogWarning("No handler for {itemType} {alias}", itemType, item.Node.GetAlias());
                continue;
            }

            options.Callbacks?.Update?.Invoke(item.Node.GetAlias(),
				CalculateProgress(index, total, options.ProgressMin, options.ProgressMax), 100);

            if (handlerPair != null)
            {
                actions.AddRange(handlerPair.Handler.ReportElement(item.Node, item.FileName, handlerPair.Settings, options));
            }

            index++;
        }

        return actions;
    }

    /// <summary>
    ///  Perform a paged Import against a given folder 
    /// </summary>
    [Obsolete("For better performance pass handler, will be removed in v15")]
    public IEnumerable<uSyncAction> ImportPartial(string folder, uSyncPagedImportOptions options, out int total)
    {
        var orderedNodes = LoadOrderedNodes(folder);
        return ImportPartial(orderedNodes, options, out total);
    }

    /// <summary>
    ///  perform an import of items from the suppled ordered node list. 
    /// </summary>
    public IEnumerable<uSyncAction> ImportPartial(IList<OrderedNodeInfo> orderedNodes, uSyncPagedImportOptions options, out int total)
    {
        lock (_importLock)
        {
            using (var pause = _mutexService.ImportPause(options.PauseDuringImport))
            {

                total = orderedNodes.Count;

                var actions = new List<uSyncAction>();
                var lastType = string.Empty;

                var range = options.ProgressMax - options.ProgressMin;

                SyncHandlerOptions syncHandlerOptions = HandlerOptionsFromPaged(options);

                HandlerConfigPair? handlerPair = null;

                var index = options.PageNumber * options.PageSize;

                using var scope = _scopeProvider.CreateNotificationScope(
                    eventAggregator: _eventAggregator,
                    loggerFactory: _loggerFactory,
                    syncConfigService: _uSyncConfig,
                    syncEventService: _mutexService,
                    backgroundTaskQueue: _backgroundTaskQueue,
                    options.Callbacks?.Update);
                {
                    try
                    {
                        foreach (var item in orderedNodes.Skip(options.PageNumber * options.PageSize).Take(options.PageSize))
                        {
                            var node = item.Node ?? XElement.Load(item.FileName);

                            var itemType = node.GetItemType();
                            if (!itemType.InvariantEquals(lastType))
                            {
                                lastType = itemType;
                                handlerPair = _handlerFactory.GetValidHandlerByTypeName(itemType, syncHandlerOptions);

                                // special case, blueprints looks like IContent items, except they are slightly different
                                // so we check for them specifically and get the handler for the entity rather than the object type.
                                if (node.IsContent() && node.IsBlueprint())
                                {
                                    lastType = UdiEntityType.DocumentBlueprint;
                                    handlerPair = _handlerFactory.GetValidHandlerByEntityType(UdiEntityType.DocumentBlueprint);
                                }
                            }

                            if (handlerPair == null)
                            {
                                _logger.LogWarning("No handler was found for {alias} item might not process correctly", itemType);
                                continue;
                            }

                            options.Callbacks?.Update?.Invoke(node.GetAlias(),
								CalculateProgress(index, total, options.ProgressMin, options.ProgressMax), 100);

                            if (handlerPair != null)
                            {
                                actions.AddRange(handlerPair.Handler.ImportElement(node, item.FileName, handlerPair.Settings, options));
                            }

                            index++;
                        }
                    }
                    finally
                    {
                        scope.Complete();
                    }

                }

                return actions;
            }
        }
    }

    /// <summary>
    ///  Perform a paged Import second pass against a given folder 
    /// </summary>
    public IEnumerable<uSyncAction> ImportPartialSecondPass(IEnumerable<uSyncAction> actions, uSyncPagedImportOptions options)
    {
        lock (_importLock)
        {
            using (var pause = _mutexService.ImportPause(options.PauseDuringImport))
            {
                SyncHandlerOptions syncHandlerOptions = HandlerOptionsFromPaged(options);

                var secondPassActions = new List<uSyncAction>();

                var total = actions.Count();

                var lastType = string.Empty;
                HandlerConfigPair? handlerPair = null;

                var index = options.PageNumber * options.PageSize;

                using (var scope = _scopeProvider.CreateNotificationScope(
                    eventAggregator: _eventAggregator,
                    loggerFactory: _loggerFactory,
                    syncConfigService: _uSyncConfig,
                    syncEventService: _mutexService,
                    backgroundTaskQueue: _backgroundTaskQueue,
                    options.Callbacks?.Update))
                {
                    try
                    {
                        foreach (var action in actions.Skip(options.PageNumber * options.PageSize).Take(options.PageSize))
                        {
                            if (action.HandlerAlias is null) continue;

                            if (!action.HandlerAlias.InvariantEquals(lastType))
                            {
                                lastType = action.HandlerAlias;
                                handlerPair = _handlerFactory.GetValidHandler(action.HandlerAlias, syncHandlerOptions);
                            }

                            if (handlerPair == null)
                            {
                                _logger.LogWarning("No handler was found for {alias} item might not process correctly", action.HandlerAlias);
                                continue;
                            }

                            options.Callbacks?.Update?.Invoke($"Second Pass: {action.Name}",
								CalculateProgress(index, total, options.ProgressMin, options.ProgressMax), 100);

                            secondPassActions.AddRange(handlerPair.Handler.ImportSecondPass(action, handlerPair.Settings, options));

                            index++;
                        }
                    }
                    finally
                    {
                        scope.Complete();
                    }
                }

                return secondPassActions;
            }
        }
    }

    /// <summary>
    ///  Perform a paged Import post import against a given folder 
    /// </summary>
    public IEnumerable<uSyncAction> ImportPartialPostImport(IEnumerable<uSyncAction> actions, uSyncPagedImportOptions options)
    {
        if (actions == null || !actions.Any()) return [];

        lock (_importLock)
        {
            using (var pause = _mutexService.ImportPause(options.PauseDuringImport))
            {

                SyncHandlerOptions syncHandlerOptions = HandlerOptionsFromPaged(options);

                var aliases = actions.Select(x => x.HandlerAlias).Distinct();

                var folders = actions
                    .Where(x => x.RequiresPostProcessing)
                    .Select(x => new { alias = x.HandlerAlias, folder = Path.GetDirectoryName(x.FileName), actions = x })
                    .DistinctBy(x => x.folder)
                    .GroupBy(x => x.alias)
                    .ToList();

                var results = new List<uSyncAction>();

                var index = 0;

                foreach (var actionItem in folders.SelectMany(actionGroup => actionGroup))
                {
                    if (actionItem.alias is null) continue;

                    var handlerPair = _handlerFactory.GetValidHandler(actionItem.alias, syncHandlerOptions);

                    if (handlerPair == null)
                    {
                        _logger.LogWarning("No handler was found for {alias} item might not process correctly", actionItem.alias);
                    }
                    else
                    {
                        if (handlerPair.Handler is ISyncPostImportHandler postImportHandler)
                        {
                            options.Callbacks?.Update?.Invoke(actionItem.alias, index, folders.Count);

                            var handlerActions = actions.Where(x => x.HandlerAlias.InvariantEquals(handlerPair.Handler.Alias));
                            results.AddRange(postImportHandler.ProcessPostImport(actionItem.folder ?? string.Empty, handlerActions, handlerPair.Settings));
                        }
                    }

                    index++;
                }

                return results;
            }
        }
    }

    /// <summary>
    ///  Perform a paged Clean after import for a given folder 
    /// </summary>
    public IEnumerable<uSyncAction> ImportPostCleanFiles(IEnumerable<uSyncAction> actions, uSyncPagedImportOptions options)
    {
        if (actions == null) return [];

        lock (_importLock)
        {
            using (var pause = _mutexService.ImportPause(options.PauseDuringImport))
            {
                SyncHandlerOptions syncHandlerOptions = new SyncHandlerOptions(
                    options.HandlerSet, options.UserId);

                var cleans = actions
                    .Where(x => x.Change == ChangeType.Clean && !string.IsNullOrWhiteSpace(x.FileName))
                    .Select(x => new { alias = x.HandlerAlias, folder = Path.GetDirectoryName(x.FileName), actions = x })
                    .DistinctBy(x => x.folder)
                    .GroupBy(x => x.alias)
                    .ToList();

                var results = new List<uSyncAction>();

                var index = 0;

                foreach (var actionItem in cleans.SelectMany(actionGroup => actionGroup))
                {
                    if (actionItem.alias is null) continue;

                    var handlerPair = _handlerFactory.GetValidHandler(actionItem.alias, syncHandlerOptions);
                    if (handlerPair is null) continue;

                    if (handlerPair.Handler is ISyncCleanEntryHandler cleanEntryHandler)
                    {
                        options.Callbacks?.Update?.Invoke(actionItem.alias, index, cleans.Count);

                        var handlerActions = actions.Where(x => x.HandlerAlias.InvariantEquals(handlerPair.Handler.Alias));
                        results.AddRange(cleanEntryHandler.ProcessCleanActions(actionItem.folder, handlerActions, handlerPair.Settings));
                    }
                    index++;
                }

                return results;
            }
        }
    }

    private static SyncHandlerOptions HandlerOptionsFromPaged(uSyncPagedImportOptions options)
        => new(options.HandlerSet, options.UserId)
        {
            IncludeDisabled = options.IncludeDisabledHandlers
        };

    /// <summary>
    ///  Load the xml in a folder in level order so we process the higher level items first.
    /// </summary>
    [Obsolete("use handler and multiple folder method will be removed in v15")]
    public IList<OrderedNodeInfo> LoadOrderedNodes(string folder)
        => LoadOrderedNodes([folder]);

    /// <summary>
    ///  Load the xml in a folder in level order so we process the higher level items first.
    /// </summary>
    [Obsolete("use handler and multiple folder method will be removed in v15")]
    public IList<OrderedNodeInfo> LoadOrderedNodes(string[] folders)
    {
        var nodes = new List<OrderedNodeInfo>();

        foreach (var folder in folders)
        {
            var files = _syncFileService.GetFiles(folder, $"*.{_uSyncConfig.Settings.DefaultExtension}", true);
            foreach (var file in files)
            {
                var xml = _syncFileService.LoadXElement(file);
                nodes.Add(new OrderedNodeInfo(
                    filename: file,
                    node: xml,
                    level: xml.GetLevel(),
                    path: file.Substring(folder.Length),
                    isRoot: false));

            }
        }

        return nodes
            .OrderBy(x => (x.Level * 1000) + x.Node.GetItemSortOrder())
            .ToList();
    }

    /// <summary>
    ///  load up ordered nodes from a handler folder, 
    /// </summary>
    /// <remarks>
    ///  this makes ordered node loading faster, when we are processing multiple requests, because we don't have to calculate it each time
    /// </remarks>
    [Obsolete("use handler and multiple folder method will be removed in v15")]
    public IList<OrderedNodeInfo> LoadOrderedNodes(ISyncHandler handler, string handlerFolder)
        => LoadOrderedNodes(handler, [handlerFolder]);

    /// <summary>
    ///  load up ordered nodes from a handler folder, 
    /// </summary>
    /// <remarks>
    ///  this makes ordered node loading faster, when we are processing multiple requests, because we don't have to calculate it each time
    /// </remarks>
    public IList<OrderedNodeInfo> LoadOrderedNodes(ISyncHandler handler, string[] handlerFolders)
        => handler.FetchAllNodes(handlerFolders).ToList();

    /// <summary>
    ///  calculate the percentage progress we are making between a range. 
    /// </summary>
    /// <remarks>
    ///  for partial imports this allows the calling progress to smooth out the progress bar.
    /// </remarks>
    private static int CalculateProgress(int value, int total, int min, int max)
        => (int)(min + (((float)value / total) * (max - min)));
}
