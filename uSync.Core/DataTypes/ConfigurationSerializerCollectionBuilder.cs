﻿using Umbraco.Cms.Core.Composing;
using Umbraco.Extensions;

namespace uSync.Core.DataTypes;

public class ConfigurationSerializerCollectionBuilder
    : LazyCollectionBuilderBase<ConfigurationSerializerCollectionBuilder, ConfigurationSerializerCollection, IConfigurationSerializer>
{
    protected override ConfigurationSerializerCollectionBuilder This => this;
}


public class ConfigurationSerializerCollection :
    BuilderCollectionBase<IConfigurationSerializer>
{
    public ConfigurationSerializerCollection(Func<IEnumerable<IConfigurationSerializer>> items)
        : base(items)
    {
    }

    public IConfigurationSerializer? GetSerializer(string editorAlias)
        => this.FirstOrDefault(x => x.Editors.InvariantContains(editorAlias));

    public IEnumerable<IConfigurationSerializer> GetSerializers(string editorAlias)
        => this.Where(x => x.Editors.InvariantContains(editorAlias));
}
