using System;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace Revivify_MeadowFix;

sealed class Options : OptionInterface
{
    public static Configurable<float> ReviveSpeed;
    public static Configurable<int> DeathsUntilExhaustion;
    public static Configurable<int> DeathsUntilComa;
    public static Configurable<int> DeathsUntilExpire;
    public static Configurable<float> CorpseExpiryTime;
    public static Configurable<bool> ReviveWithProximity;
    public static Configurable<float> ReviveDistance;

    public Options()
    {
        ReviveSpeed = config.Bind("cfgReviveSpeed", 1f, new ConfigAcceptableRange<float>(0.1f, 5f));
        DeathsUntilExhaustion = config.Bind("cfgDeathsUntilExhaustion", 1, new ConfigAcceptableRange<int>(1, 10));
        DeathsUntilComa = config.Bind("cfgDeathsUntilComa", 2, new ConfigAcceptableRange<int>(1, 10));
        DeathsUntilExpire = config.Bind("cfgDeathsUntilExpire", 3, new ConfigAcceptableRange<int>(1, 10));
        CorpseExpiryTime = config.Bind("cfgCorpseExpiryTime", 1.5f, new ConfigAcceptableRange<float>(0.05f, 10f));
        ReviveWithProximity = config.Bind("cfgReviveWithProximity", false, new ConfigurableInfo("Determines whether revival occurs by performing CPR or by simply standing within proximity of the dead player."));
        ReviveDistance = config.Bind("cfgReviveDistance", 60f, new ConfigAcceptableRange<float>(1f, 120f));
    }

    public override void Initialize()
    {
        base.Initialize();

        Tabs = new OpTab[] { new OpTab(this, "") };

        float sliderX = 270;
        float y = 390;

        var author = new OpLabel(20, 600 - 40, "Original by Dual, Proximity by Daimyo, Combination/Update by Auxiar Molkhun", true);
        var github = new OpLabel(20, 600 - 40 - 40, "github.com/Auxiar/Revivify_MeadowFix");

        var d1 = new OpLabel(new(220, y), Vector2.zero, "Revive speed multiplier", FLabelAlignment.Right);
        var s1 = new OpFloatSlider(ReviveSpeed, new Vector2(sliderX, y - 6), 300, decimalNum: 1);

        var d2 = new OpLabel(new(220, y -= 60), Vector2.zero, "Deaths until exhaustion", FLabelAlignment.Right);
        var s2 = new OpSlider(DeathsUntilExhaustion, new Vector2(sliderX, y - 6), 300);

        var d3 = new OpLabel(new(220, y -= 60), Vector2.zero, "Deaths until slugpups become comatose", FLabelAlignment.Right);
        var s3 = new OpSlider(DeathsUntilComa, new Vector2(sliderX, y - 6), 300);

        var d4 = new OpLabel(new(220, y -= 60), Vector2.zero, "Deaths until slugpups permanently expire", FLabelAlignment.Right);
        var s4 = new OpSlider(DeathsUntilExpire, new Vector2(sliderX, y - 6), 300);

        var d5 = new OpLabel(new(220, y -= 60), Vector2.zero, "Time until bodies expire, in minutes", FLabelAlignment.Right);
        var s5 = new OpFloatSlider(CorpseExpiryTime, new Vector2(sliderX, y - 6), 300, decimalNum: 1);

        var d6 = new OpLabel(new(220, y -= 60), Vector2.zero, "Revive using Proximity", FLabelAlignment.Right);
        var s6 = new OpCheckBox(ReviveWithProximity, new Vector2(sliderX, y -6));
        
        var d7 = new OpLabel(new Vector2(220f, y -= 60f), Vector2.zero, "Distance to revive (60f = spear length)", FLabelAlignment.Right);
        var s7 = new OpFloatSlider(ReviveDistance, new Vector2(sliderX, y - 6), 300, decimalNum: 1);

        Tabs[0].AddItems(new UIelement[]
            {
                author,
                github,
                d1,
                s1,
                d2,
                s2,
                d3,
                s3,
                d4,
                s4,
                d5,
                s5,
                d6,
                s6,
                d7,
                s7
            });
    }
}
