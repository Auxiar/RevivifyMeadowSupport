using Menu.Remix.MixedUI;
using UnityEngine;

namespace RevivifyMeadowSupport;

sealed class Options : OptionInterface
{
    public static Configurable<string> ReviveMode;
    public static Configurable<float> ReviveSpeed;
    public static Configurable<int> DeathsUntilExhaustion;
    public static Configurable<int> DeathsUntilComa;
    public static Configurable<int> DeathsUntilExpire;
    public static Configurable<float> CorpseExpiryTime;
    public static Configurable<bool> DisableInArena;
    public static Configurable<bool> DisableInSandbox;
    public static Configurable<bool> AllowPupsToReviveYou;
    public static Configurable<float> ReviveDistance;
    public static Configurable<bool> AllowCorpsePiggyback;

    public Options()
    {
        ReviveMode = config.Bind("cfgReviveMode", "CPR", new ConfigAcceptableList<string>("CPR", "Proximity"));
        ReviveSpeed = config.Bind("cfgReviveSpeed", 1f, new ConfigAcceptableRange<float>(0.1f, 5f));
        DeathsUntilExhaustion = config.Bind("cfgDeathsUntilExhaustion", 1, new ConfigAcceptableRange<int>(0, 10));
        DeathsUntilComa = config.Bind("cfgDeathsUntilComa", 2, new ConfigAcceptableRange<int>(0, 10));
        DeathsUntilExpire = config.Bind("cfgDeathsUntilExpire", 3, new ConfigAcceptableRange<int>(0, 10));
        CorpseExpiryTime = config.Bind("cfgCorpseExpiryTime", 2f, new ConfigAcceptableRange<float>(0f, 10f));
        DisableInArena = config.Bind("cfgAutoDisableInArena", true);
        DisableInSandbox = config.Bind("cfgAutoDisableInSandbox", true);
        AllowPupsToReviveYou = config.Bind("cfgAllowPupsToReviveOthers", true);
        ReviveDistance = config.Bind("cfgReviveDistance", 40f, new ConfigAcceptableRange<float>(1f, 120f));
        AllowCorpsePiggyback = config.Bind("cfgAllowCorpsePiggyback", true);
    }
    
    static class Descriptions
    {
        public const string reviveMode = "Revive method required when YOU die. Also affects SP/Jolly revives. CPR: lay corpse on flat, dry ground and press Grab to compress. Proximity: stand within the proximity distance until they wake up.";
        public const string reviveSpeed = "Controls how fast YOU are revived - higher values mean fewer compressions/less time. Also affects the speed of revives you do in SP/Jolly co-op.";
        public const string deathsUntilExhaustion = "Like being starved - you tire easily and are left vulnerable. You'll appear a paler shade of your color. Set to 0 to disable, meaning deaths can't cause exhaustion.";
        public const string deathsUntilComa = "Slugpups go comatose instead of exhausted - they appear knocked out and must be carried to the next shelter to survive. Hold Grab to piggyback them (if enabled).";
        public const string deathsUntilExpire = "Once this death count is reached, you can no longer be revived. Set to 0 to disable permadeath entirely.";
        public const string corpseExpiryTime = "How long (in minutes) a scug can be dead before they can no longer be revived. Set to 0 to disable, allowing corpses to be revived at any time.";
        public const string disableInArena = "When checked, the mod automatically disables all functionality in Arena mode - no restart required to toggle the mod now!";
        public const string disableInSandbox = "When checked, the mod automatically disables all functionality in Sandbox mode - Mainly for SP/Jolly play";
        public const string allowPupsToReviveYou = "When checked, allows pups to revive you (or others in SP/Jolly) using proximity - Mainly for SP/Jolly play. This does NOT override their behavior to try and revive you, it simply allows them to do so";
        public const string reviveDistance = "How close someone must be to YOU to trigger proximity revival. The default of 40 requires standing quite close.";
        public const string allowCorpsePiggyback = "In SP/Jolly, prevents piggybacking a corpse when using Proximity Revival, making it harder to safely carry them. Just for some added challenge.";
    }

    private UIelement[] ProximityMenu;
    private UIelement[] CprMenu;

    public override void Initialize()
    {
        base.Initialize();
        Tabs = new OpTab[]
        {
            new OpTab(this, "")
        };

        float x = 270;
        float y = 475;

        float optionSpacing = 37;

        var author = new OpLabel(20, 600 - 40,
            "Original by Dual, Proximity Revival by Daimyo, Combination/Update by Auxiar Molkhun");
        var github = new OpLabel(20, 600 - 40 - 40, "github.com/Auxiar/RevivifyMeadowSupport");

        var d1 = new OpLabel(new(x - 50, y), Vector2.zero, "Your Revival Method", FLabelAlignment.Right);
        var s1 = new OpComboBox(ReviveMode, new Vector2(x, y - 6), 120f, new string[]{ "CPR", "Proximity" });
        d1.description = Descriptions.reviveMode;
        s1.description = Descriptions.reviveMode;

        var d2 = new OpLabel(new(x - 50, y -= optionSpacing * 2), Vector2.zero, "Revive speed multiplier", FLabelAlignment.Right);
        var s2 = new OpFloatSlider(ReviveSpeed, new Vector2(x, y - 6), 300, decimalNum: 1);
        d2.description = Descriptions.reviveSpeed;
        s2.description = Descriptions.reviveSpeed;

        var d3 = new OpLabel(new(x - 50, y -= optionSpacing), Vector2.zero, "Deaths until exhaustion", FLabelAlignment.Right);
        var s3 = new OpSlider(DeathsUntilExhaustion, new Vector2(x, y - 6), 300);
        d3.description = Descriptions.deathsUntilExhaustion;
        s3.description = Descriptions.deathsUntilExhaustion;

        var d4 = new OpLabel(new(x - 50, y -= optionSpacing), Vector2.zero, "Deaths until slugpups become comatose", FLabelAlignment.Right);
        var s4 = new OpSlider(DeathsUntilComa, new Vector2(x, y - 6), 300);
        d4.description = Descriptions.deathsUntilComa;
        s4.description = Descriptions.deathsUntilComa;

        var d5 = new OpLabel(new(x - 50, y -= optionSpacing), Vector2.zero, "Deaths until permadeath", FLabelAlignment.Right);
        var s5 = new OpSlider(DeathsUntilExpire, new Vector2(x, y - 6), 300);
        d5.description = Descriptions.deathsUntilExpire;
        s5.description = Descriptions.deathsUntilExpire;

        var d6 = new OpLabel(new(x - 50, y -= optionSpacing), Vector2.zero, "Time until permadeath", FLabelAlignment.Right);
        var s6 = new OpFloatSlider(CorpseExpiryTime, new Vector2(x, y - 6), 300, decimalNum: 1);
        d6.description = Descriptions.corpseExpiryTime;
        s6.description = Descriptions.corpseExpiryTime;
        
        var d7 = new OpLabel(new Vector2(x - 50, y -= optionSpacing), Vector2.zero, "Disable in Arena Mode", FLabelAlignment.Right);
        var s7 = new OpCheckBox(DisableInArena, new Vector2(x, y - 6));
        d7.description = Descriptions.disableInArena;
        s7.description = Descriptions.disableInArena;
        
        var d8 = new OpLabel(new Vector2(x - 50, y -= optionSpacing), Vector2.zero, "Disable in Sandbox Mode", FLabelAlignment.Right);
        var s8 = new OpCheckBox(DisableInSandbox, new Vector2(x, y - 6));
        s8.description = Descriptions.disableInSandbox;
        d8.description = Descriptions.disableInSandbox;
        
        var d9 = new OpLabel(new Vector2(x - 50, y -= optionSpacing), Vector2.zero, "Allow Pups to Revive You", FLabelAlignment.Right);
        var s9 = new OpCheckBox(AllowPupsToReviveYou, new Vector2(x, y - 6));
        d9.description = Descriptions.allowPupsToReviveYou;
        s9.description = Descriptions.allowPupsToReviveYou;

        var d10 = new OpLabel(new Vector2(x - 50, y -= optionSpacing), Vector2.zero, "Proximity revive distance", FLabelAlignment.Right);
        var s10 = new OpFloatSlider(ReviveDistance, new Vector2(x, y - 6), 300, decimalNum: 1);
        d10.description = Descriptions.reviveDistance;
        s10.description = Descriptions.reviveDistance;

        var d11 = new OpLabel(new(x - 50, y -= optionSpacing), Vector2.zero, "Allow corpse piggyback with proximity", FLabelAlignment.Right);
        var s11 = new OpCheckBox(AllowCorpsePiggyback, new Vector2(x, y - 6));
        d11.description = Descriptions.allowCorpsePiggyback;
        s11.description = Descriptions.allowCorpsePiggyback;

        ProximityMenu = new UIelement[]
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
            s7,
            d8,
            s8,
            d9,
            s9,
            d10,
            s10,
            d11,
            s11
        };

        CprMenu = new UIelement[]
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
            s7,
            d8,
            s8,
            d9,
            s9
        };

        Tabs[0].AddItems(ReviveMode.Value == "CPR" ? CprMenu : ProximityMenu);

        s1.OnValueChanged += delegate {SwitchMenues(Tabs[0], s1.value != "CPR");};
        s9.OnValueChanged += delegate { SwitchMenues(Tabs[0], s9.value == "true"); };
    }

    private void SwitchMenues(OpTab tab, bool showProximity)
    {
        if (showProximity)
        {
            tab.RemoveItems(CprMenu);
            tab.AddItems(ProximityMenu);  
        }
        else
        {        
            tab.RemoveItems(ProximityMenu);
            tab.AddItems(CprMenu);      
        }
    }
    
    
}
