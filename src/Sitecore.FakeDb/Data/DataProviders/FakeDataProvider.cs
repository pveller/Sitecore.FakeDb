﻿namespace Sitecore.FakeDb.Data.DataProviders
{
  using System.Collections.Generic;
  using System.Linq;
  using System.Threading;
  using Sitecore.Collections;
  using Sitecore.Data;
  using Sitecore.Data.DataProviders;
  using Sitecore.Data.Query;
  using Sitecore.Data.Templates;
  using Sitecore.Diagnostics;
  using Sitecore.FakeDb.Data.Engines;
  using Sitecore.Globalization;
  using CallContext = Sitecore.Data.DataProviders.CallContext;
  using Version = Sitecore.Data.Version;

  public class FakeDataProvider : DataProvider
  {
    private readonly ThreadLocal<Dictionary<string, string>> properties = new ThreadLocal<Dictionary<string, string>>();

    private readonly DataStorage dataStorage;


    public FakeDataProvider()
    {
    }

    public FakeDataProvider(DataStorage dataStorage)
    {
      this.dataStorage = dataStorage;
    }

    public virtual DataStorage DataStorage
    {
      get { return this.dataStorage ?? DataStorageSwitcher.CurrentValue(this.Database.Name); }
    }

    public override bool ChangeTemplate(ItemDefinition itemDefinition, TemplateChangeList changes, CallContext context)
    {
      Assert.ArgumentNotNull(itemDefinition, "itemDefinition");
      Assert.ArgumentNotNull(changes, "changes");

      var item = this.DataStorage.GetFakeItem(itemDefinition.ID);
      Assert.IsNotNull(item, "Unable to change item template. The item '{0}' is not found.", itemDefinition.ID);
      Assert.IsNotNull(changes.Target, "Unable to change item template. The target template is not found.");

      item.TemplateID = changes.Target.ID;
      return true;
    }

    public override IdCollection GetTemplateItemIds(CallContext context)
    {
      if (this.DataStorage == null)
      {
        return new IdCollection();
      }

      var ids = this.DataStorage.GetFakeTemplates().Select(t => t.ID).ToArray();

      return new IdCollection { ids };
    }

    public override ItemDefinition GetItemDefinition(ID itemId, CallContext context)
    {
      if (this.DataStorage == null)
      {
        return null;
      }

      var item = this.DataStorage.GetFakeItem(itemId);

      return item != null ? new ItemDefinition(itemId, item.Name, item.TemplateID, ID.Null) : null;
    }

    public override VersionUriList GetItemVersions(ItemDefinition itemDefinition, CallContext context)
    {
      var list = new List<VersionUri>();
      var versions = new VersionUriList();

      var item = this.DataStorage.GetFakeItem(itemDefinition.ID);
      if (item == null)
      {
        return versions;
      }

      foreach (var field in item.Fields)
      {
        foreach (var fieldLang in field.Values)
        {
          var language = fieldLang.Key;

          foreach (var fieldVer in fieldLang.Value)
          {
            var version = fieldVer.Key;

            if (list.Any(l => l.Language.Name == language && l.Version.Number == version))
            {
              continue;
            }

            list.Add(new VersionUri(Language.Parse(language), new Version(version)));
          }
        }
      }

      foreach (var version in list)
      {
        versions.Add(version);
      }

      return versions;
    }

    public override FieldList GetItemFields(ItemDefinition itemDefinition, VersionUri versionUri, CallContext context)
    {
      var storage = this.DataStorage;
      var item = storage.GetFakeItem(itemDefinition.ID);
      if (item == null)
      {
        return null;
      }

      return storage.BuildItemFieldList(item, itemDefinition.TemplateID, versionUri.Language, versionUri.Version);
    }

    public override TemplateCollection GetTemplates(CallContext context)
    {
      var templates = new TemplateCollection();

      if (this.DataStorage == null)
      {
        return templates;
      }

      foreach (var ft in this.DataStorage.GetFakeTemplates())
      {
        templates.Add(this.BuildTemplate(ft, templates));
      }

      return templates;
    }

    public override LanguageCollection GetLanguages(CallContext context)
    {
      return new LanguageCollection { Language.Parse("en") };
    }

    public override ID SelectSingleID(string query, CallContext context)
    {
      query = query.Replace("fast:", string.Empty);
      var item = Query.SelectSingleItem(query, this.Database);

      return item != null ? item.ID : ID.Null;
    }

    public override IDList SelectIDs(string query, CallContext context)
    {
      query = query.Replace("fast:", string.Empty);
      var items = Query.SelectItems(query, this.Database);

      return items != null ? IDList.Build(items.Select(i => i.ID).ToArray()) : new IDList();
    }

    /// <summary>
    /// Sets the property.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="value">The property value.</param>
    /// <param name="context">The context. Ignored.</param>
    /// <returns>Always True.</returns>
    public override bool SetProperty(string name, string value, CallContext context)
    {
      Assert.ArgumentNotNull(name, "name");
      var currentProp = this.properties.Value;
      if (currentProp == null)
      {
        this.properties.Value = new Dictionary<string, string> { { name, value } };
      }
      else
      {
        this.properties.Value[name] = value;
      }

      return true;
    }

    /// <summary>
    /// Get the property.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="context">The context. Ignored.</param>
    /// <returns>The property value if exists. Otherwise null.</returns>
    public override string GetProperty(string name, CallContext context)
    {
      Assert.ArgumentNotNull(name, "name");
      var currentProp = this.properties.Value;
      if (currentProp == null)
      {
        return null;
      }

      return currentProp.ContainsKey(name) ? currentProp[name] : null;
    }

    protected virtual Template BuildTemplate(DbTemplate ft, TemplateCollection templates)
    {
      var builder = new Template.Builder(ft.Name, ft.ID, templates);

      var sectionName = "Data";
      var sectionId = ID.NewID;

      var sectionItem = ft.Children.FirstOrDefault(i => i.TemplateID == TemplateIDs.TemplateSection);
      if (sectionItem != null)
      {
        sectionName = sectionItem.Name;
        sectionId = sectionItem.ID;
      }

      var section = builder.AddSection(sectionName, sectionId);

      foreach (var field in ft.Fields)
      {
        if (ft.ID != TemplateIDs.StandardTemplate && field.IsStandard())
        {
          continue;
        }

        var newField = section.AddField(field.Name, field.ID);
        newField.SetShared(field.Shared);
        newField.SetType(field.Type);
        newField.SetSource(field.Source);
      }

      if (ft.ID != TemplateIDs.StandardTemplate)
      {
        builder.SetBaseIDs(ft.BaseIDs.Any() ? string.Join("|", ft.BaseIDs as IEnumerable<ID>) : TemplateIDs.StandardTemplate.ToString());
      }

      return builder.Template;
    }
  }
}