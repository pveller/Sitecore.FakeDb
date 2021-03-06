﻿namespace Sitecore.FakeDb.Links
{
  using Sitecore.Common;
  using Sitecore.Links;

  public class LinkProviderSwitcher : Switcher<LinkProvider>
  {
#if !SC72
    private readonly Sitecore.Links.LinkProviderSwitcher providerSwitcher;
#endif

    public LinkProviderSwitcher(LinkProvider linkProviderToSwitchTo)
      : base(linkProviderToSwitchTo)
    {
#if !SC72
      this.providerSwitcher = new Sitecore.Links.LinkProviderSwitcher("switcher");
#endif
    }

    public override void Dispose()
    {
#if !SC72
      this.providerSwitcher.Dispose();
#endif
      base.Dispose();
    }
  }
}