﻿using System.Collections.Generic;

using Umbraco.Cms.Core.Services;

using uSync.Core.DataTypes;

namespace uSync8.Community.DataTypeSerializers.CoreTypes;

public class MNTPickerConfigSerializer : SyncDataTypeSerializerBase, IConfigurationSerializer
{
    public MNTPickerConfigSerializer(IEntityService entityService)
        : base(entityService)
    { }

    public string Name => "MNTPNodeSerializer";

    public string[] Editors => ["Umbraco.MultiNodeTreePicker" ];

    public override IDictionary<string, object> GetConfigurationExport(IDictionary<string, object> configuration)
    {
        return base.GetConfigurationExport(configuration);
    }

    public override IDictionary<string, object> GetConfigurationImport(IDictionary<string, object> configuration)
    {
        return base.GetConfigurationImport(configuration);
    }

    //public override string SerializeConfig(object configuration)
    //{
    //    var MNTPMappedConfig = new MappedPathConfigBase<MultiNodePickerConfiguration>();

    //    if (configuration is MultiNodePickerConfiguration pickerConfig)
    //    {
    //        MNTPMappedConfig.Config = new MultiNodePickerConfiguration()
    //        {
    //            IgnoreUserStartNodes = pickerConfig.IgnoreUserStartNodes,
    //            // Filter = pickerConfig.Filter,
    //            MaxNumber = pickerConfig.MaxNumber,
    //            MinNumber = pickerConfig.MinNumber,
    //            // ShowOpen = pickerConfig.ShowOpen,
    //            TreeSource = new MultiNodePickerConfigurationTreeSource()
    //            {
    //                ObjectType = pickerConfig.TreeSource.ObjectType,
    //                StartNodeId = pickerConfig.TreeSource.StartNodeId,
    //                StartNodeQuery = pickerConfig.TreeSource.StartNodeQuery
    //            }

    //        };

    //        if (pickerConfig?.TreeSource?.StartNodeId != null)
    //        {
    //            MNTPMappedConfig.MappedPath = UdiToEntityPath(pickerConfig.TreeSource.StartNodeId);
    //        }

    //        return base.SerializeConfig(MNTPMappedConfig);
    //    }

    //    return base.SerializeConfig(configuration);
    //}


    //public override object DeserializeConfig(string config, Type configType)
    //{
    //    if (configType == typeof(MultiNodePickerConfiguration))
    //    {
    //        var mappedConfig = config.DeserializeJson<MappedPathConfigBase<MultiNodePickerConfiguration>>();

    //        if (!string.IsNullOrWhiteSpace(mappedConfig.MappedPath))
    //        {
    //            mappedConfig.Config.TreeSource.StartNodeId = PathToUdi(mappedConfig.MappedPath);
    //        }

    //        return mappedConfig.Config;
    //    }

    //    return base.DeserializeConfig(config, configType);
    //}
}
