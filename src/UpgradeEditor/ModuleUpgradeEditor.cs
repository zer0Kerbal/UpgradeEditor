//\//////////////////////////////////////////////////////////////////////////////
//  Upgrade Editor - KSP Addon                                                 //
//																			   //
//	Copyright(C) 2017  NotTheRealRMS                                           //
//																			   //
//  This program is free software: you can redistribute it and/or modify       //
//	it under the terms of the GNU General Public License as published by       //
//	the Free Software Foundation, either version 3 of the License, or          //
//	(at your option) any later version.                                        //
//                                                                             //
//	This program is distributed in the hope that it will be useful,            //
//  but WITHOUT ANY WARRANTY; without even the implied warranty of             //
//	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the                //
//	GNU General Public License for more details.                               //
//                                                                             //
//	You should have received a copy of the GNU General Public License          //
//	along with this program.If not, see<https://www.gnu.org/licenses/>.        //
//\//////////////////////////////////////////////////////////////////////////////

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

namespace UpgradeEditor
{
	[KSPAddon(KSPAddon.Startup.EditorAny, false)]
	public class ModuleUpgradeEditor : PartModule
	{

		// Constant values

		const float width = 800f;
		const float height = 600f;
		const float button_width = 160f;
		const float button_height = 100f;

        readonly List<ConfigNode> allupgrades = new List<ConfigNode>(); //Those located in multiple PartModules will be listed only once.

        // The cached part config for the selected part without upgrades applied.
        ConfigNode zupgradespart;

		// GUI elements. Some must be created as lists to allow for dynamic interface elements matching the number of upgrades.

		// The core of the interface, placed into a MultiOptionDialog
		List<DialogGUIBase> upgrade_editor = new List<DialogGUIBase>();

		// Lists each PartUpgrade that can be toggled on/off
		List<DialogGUIHorizontalLayout> upgrade_line = new List<DialogGUIHorizontalLayout>();

		// Sets the scroll where each line of upgrade buttons and their info is placed
		DialogGUIBase[] scrollList;

        // Persistent setting on whether to start with all available upgrades installed or not.
        [KSPField(isPersistant = true)]
        static bool ToggleAllUpgrades;

        // Works both ways, but unlike the above, does not override everything. Renamed to Enable instead of Disable and reversed
        // now that ToggleAllUpgrades is off by default. This way, it also fixes previous bugs involving the first part to be processed.
        [KSPField(isPersistant = true)]
        static bool EnableAllUpgrades;

        // The names of upgrades that are temporarily disabled in a single line so it can be stored in a saved craft. Defaults to None.
        [KSPField(isPersistant = true)]
        string TemporarilyDisabledUpgrades = string.Empty;

        // A list of upgrades that should be ignored because disabling them is mostly pointless. IE: max diameter changes for procedural parts.
        [KSPField(isPersistant = true)]
        string UpgradesToIgnore = string.Empty;

        // The names of upgrades that are temporarily disabled, gets it by converting the string above into a list.
        static readonly List<string> ListOfTemporarilyDisabledUpgrades = new List<string>();

        [KSPEvent(guiName = "Upgrade Editor", guiActiveEditor = true, guiActive = false, isPersistent = true)]
		public void CallUpgradeEditor()
		{
            zupgradespart = part.partInfo.partConfig;
            ToggleAllUpgrades = PartUpgradeHandler.AllEnabled = true;
			ListSelectedPartUpgrades();
			// With the list done, it's time to switch AllEnabled to Off
			ToggleAllUpgrades = PartUpgradeHandler.AllEnabled = false;
			GenerateUpgradeEditorUI();
		}

        public void Start()
        {
            PartUpgradeHandler.AllEnabled = ToggleAllUpgrades = false;
        }

        // Setting upgrades to apply on all parts in the editor when they are initialized.
        public override void OnStart(StartState state)
        {
            PartUpgradeHandler.AllEnabled = ToggleAllUpgrades = false;
            UpgradesToIgnore = ReadStringInPartModule("ModuleUpgradeEditor", "UpgradesToIgnore", part.partInfo.partConfig);
            if (!string.IsNullOrEmpty(TemporarilyDisabledUpgrades) && TemporarilyDisabledUpgrades != "None")
            {
                var ltdu = SplitSingleLineToList(TemporarilyDisabledUpgrades, ',');
                if (ltdu.Count == 0 || ltdu == null)
					return;
                zupgradespart = part.partInfo.partConfig;

                ListOfTemporarilyDisabledUpgrades.Clear();
                ToggleAllUpgrades = PartUpgradeHandler.AllEnabled = true;
                ListSelectedPartUpgrades();
                ToggleAllUpgrades = PartUpgradeHandler.AllEnabled = false;
                ReloadPartModulesConfigs();
                ReapplyIgnoredUpgrades();

                // Using RegeneratePartUpgrades if all upgrades were disabled prevents reverting to original specs in some Modules.

                var dcount = 0;
                foreach (ConfigNode upX in allupgrades)
                {
                    if (ltdu.Contains(upX.GetValue("name__")))
                    {
                        ListOfTemporarilyDisabledUpgrades.Add(upX.GetValue("name__"));
                        dcount += 1;
                    }
                    PartUpgradeManager.Handler.SetEnabled(upX.GetValue("name__"), false);
                }
                foreach (ConfigNode upX in allupgrades)
                {
                    // Limiting the runs of RegeneratePartUpgrades for a potentially irrelevant performance boost was causing bugs
                    // var checkEnabled = PartUpgradeManager.Handler.IsEnabled(upX.GetValue("name__"));
                    PartUpgradeManager.Handler.SetEnabled(upX.GetValue("name__"), !ListOfTemporarilyDisabledUpgrades.Contains(upX.GetValue("name__")));
                    if (dcount < allupgrades.Count())
                        RegeneratePartUpgrades(upX.GetValue("name__"));
                }
				// This proved necessary too. For some unknown reason PartStatsUpgradeModule retained some disabled upgrades
				// when loading saved crafts that had all their upgrades disabled, so in that case, the reset happens twice.
				if (dcount >= allupgrades.Count())
					ReloadPartStats();
            }
            //ToggleAllUpgrades = PartUpgradeHandler.AllEnabled = true;
        }

		public string ReadStringInPartModule(string ModuleName, string FieldName, ConfigNode PartConfig)
        {
			ConfigNode[] zzupgradesmodule = null;

            if (PartConfig != null)
			{
                zzupgradesmodule = PartConfig.GetNodes("MODULE");
			}
            ConfigNode ChosenModule = null;
            foreach (ConfigNode n in zzupgradesmodule)
            {
                if (n.GetValue("name") == ModuleName)
                    ChosenModule = n;
            }
            if (ChosenModule == null || string.IsNullOrEmpty(ChosenModule.GetValue(FieldName)))
                return string.Empty;
            return ChosenModule.GetValue(FieldName);
        }

		// KSP can't store a List<string> directly to a saved Part config, doing this is easier than creating a new IConfigNode
		/// <summary>
        /// Splits a single line string with values separated by a char like ',' into a list.
		/// </summary>
		List<string> SplitSingleLineToList(string singleline, char separator)
        {
            if (string.IsNullOrEmpty(singleline))
                return new List<string>();
            return singleline.Split(separator).ToList();
        }

		/// <summary>
		/// Converts a list into a single line with each value separated by ',' etc.
		/// </summary>
        string MergeListToSingleLine(List<string> stringlist, char separator)
        {
            if (stringlist == null || !stringlist.Any())
                return "";
            if (string.IsNullOrEmpty(stringlist[0]))
                return "";
            var singlelinefield = stringlist[0];
            for (int count = 1;count < stringlist.Count();count++)
            {
                singlelinefield += separator+stringlist[count];
            }
            return singlelinefield;

        }

		/// <summary>
		/// Lists all unique part upgrades available for the Part selected in the Editor.
		/// </summary>
		public void ListSelectedPartUpgrades()
		{
			allupgrades.Clear();
			if (part == null)
                return;
            var upgradealreadyinlist = false;
            var upti = SplitSingleLineToList(UpgradesToIgnore, ',');
            var allupgradesnames = new List<string>();
			foreach (PartModule pmd in part.Modules)
			{
				if (pmd.HasUpgrades())
				{
					foreach (ConfigNode upg in pmd.upgrades)
					{                        
						var upgradename = upg.GetValue("name__");
                        upgradealreadyinlist |= allupgradesnames.Contains(upgradename);
						// Unique and available without excluding temporarily locked upgrades.
                        if (!upgradealreadyinlist && !upti.Contains(upgradename) && (PartUpgradeManager.Handler.IsUnlocked(upgradename)||ListOfTemporarilyDisabledUpgrades.Contains(upgradename)))
						{
                            allupgradesnames.Add(upgradename);
							allupgrades.Add(upg);
						}
					}
				}
			}
            allupgradesnames.Clear();
			//Debug.Log("Upgrade Editor: Number of Upgrades for " + part.name + ":" + allupgrades.Count);		
		}

		void ResetUnlockedUpgrades()
        {
			if (ListOfTemporarilyDisabledUpgrades.Count > 0)
			{
                ReloadPartModulesConfigs();
                ReapplyIgnoredUpgrades();
				foreach (string upx in ListOfTemporarilyDisabledUpgrades)
				{
					if (PartUpgradeManager.Handler.IsUnlocked(upx) == false)
						continue;
                    PartUpgradeManager.Handler.SetEnabled(upx, true);
                    RegeneratePartUpgrades(upx);
				}
                ListOfTemporarilyDisabledUpgrades.Clear();
                TemporarilyDisabledUpgrades = "None";
			}
            ToggleAllUpgrades = PartUpgradeHandler.AllEnabled = true;
        }

        void OnDestroy()
        {
        	Dismiss();
        }
        public void Dismiss()
        {
            if (!ListOfTemporarilyDisabledUpgrades.Any() || string.IsNullOrEmpty(ListOfTemporarilyDisabledUpgrades[0]))
                TemporarilyDisabledUpgrades = "None";
            if (ListOfTemporarilyDisabledUpgrades.Any() && !string.IsNullOrEmpty(ListOfTemporarilyDisabledUpgrades[0]))
                TemporarilyDisabledUpgrades = MergeListToSingleLine(ListOfTemporarilyDisabledUpgrades, ',');
			//ToggleAllUpgrades = PartUpgradeHandler.AllEnabled = true;
            Destroy(this);
        }

		/// <summary>
		/// Reloads the original Part Stats without the changes from PartStatsUpgradeModule
		/// </summary>
		void ReloadPartStats()
        {
            var hasPSUM = false;
            foreach (PartModule checkforPSUM in part.Modules)
            {
                hasPSUM |= checkforPSUM.moduleName == "PartStatsUpgradeModule";
            }
            if (hasPSUM == false)
                return;
            
            if (zupgradespart != null)
            {
                // Temporary config to revert typical changes from PartStatsUpgradeModule. A generic check for everything isn't worth trying.
                var originalstats = new ConfigNode("PartStats");
                originalstats.AddValue("mass", "0");
                originalstats.AddValue("cost", "0");
                //if (!string.IsNullOrEmpty(zupgradespart.GetValue("mass")))
                    //originalstats.AddValue("mass", zupgradespart.GetValue("mass"));

                //if (!string.IsNullOrEmpty(zupgradespart.GetValue("cost")))
                    //originalstats.AddValue("cost", zupgradespart.GetValue("cost"));

                // If the original cost wasn't loaded in partInfo.partConfig, this will get it.
                //if (string.IsNullOrEmpty(originalstats.GetValue("cost")) && !string.IsNullOrEmpty(part.partInfo.cost.ToString()))
                    //originalstats.AddValue("cost", part.partInfo.cost.ToString());

                //if (!string.IsNullOrEmpty(zupgradespart.GetValue("maxTemp")))
                    //originalstats.AddValue("maxTemp", zupgradespart.GetValue("maxTemp"));

                //if (!string.IsNullOrEmpty(zupgradespart.GetValue("skinMaxTemp")))
                    //originalstats.AddValue("skinMaxTemp", zupgradespart.GetValue("skinMaxTemp"));

                //if (!string.IsNullOrEmpty(originalstats.GetValue("maxTemp")) && string.IsNullOrEmpty(originalstats.GetValue("skinMaxTemp")))
                    //originalstats.AddValue("skinMaxTemp", originalstats.GetValue("maxTemp"));
                //Debug.Log(originalstats);

                // This workaround works. That's all.
                foreach (ConfigNode upDisabled in allupgrades)
                {
                    if (ListOfTemporarilyDisabledUpgrades.Contains(upDisabled.GetValue("name__")))
                    {
                        var tempDummyUpgrade = zupgradespart.GetNode("MODULE", "name", "PartStatsUpgradeModule");
                        tempDummyUpgrade.GetNode("UPGRADES").GetNode("UPGRADE", "name__", upDisabled.GetValue("name__")).RemoveNode("PartStats");
                        tempDummyUpgrade.GetNode("UPGRADES").GetNode("UPGRADE", "name__", upDisabled.GetValue("name__")).AddNode("PartStats", originalstats);
                        //Debug.Log(tempDummyUpgrade);
                        foreach (PartModule moduleToOverrideUpgradeOn in part.Modules)
                        {
                            if (moduleToOverrideUpgradeOn.moduleName == "PartStatsUpgradeModule")
                            {
                                moduleToOverrideUpgradeOn.Load(tempDummyUpgrade);
                                PartUpgradeManager.Handler.SetEnabled(upDisabled.GetValue("name__"), true);
                                moduleToOverrideUpgradeOn.FindUpgrades(true);
                                moduleToOverrideUpgradeOn.ApplyUpgrades(StartState.Editor);
                                PartUpgradeManager.Handler.SetEnabled(upDisabled.GetValue("name__"), false);
                                tempDummyUpgrade = zupgradespart.GetNode("MODULE", "name", "PartStatsUpgradeModule");
                                moduleToOverrideUpgradeOn.Load(tempDummyUpgrade);
                                break;
                            }
                        }
                        break;
                    }
                }
            }
        }
		/// <summary>
		/// Reloads the Part Modules Configs without upgrades
		/// </summary>
        void ReloadPartModulesConfigs()
        {
            ReloadPartStats();

			ConfigNode[] zupgradesmodule = null;

            if (zupgradespart != null)
                zupgradesmodule = zupgradespart.GetNodes("MODULE");

			int nodeindex = -1;
			foreach (PartModule mmm in part.Modules)
			{
				nodeindex += 1;
                var loadthis = false;
                foreach (ConfigNode upgrade in allupgrades)
                {
                    loadthis |= mmm.upgrades.Contains(upgrade);
                }
                if (!loadthis)
					continue;
                //Debug.Log("Upgrade Editor: Resetting ConfigNode for Module " + mmm.moduleName);

                // OnAwake() breaks ModuleRCS for some reason. Perhaps it's not necessary at all, but leaving it otherwise for now.

                if(!mmm.moduleName.Contains("ModuleRCS") && mmm.moduleName != "PartStatsUpgradeModule")
                    mmm.OnAwake();
				mmm.Load(zupgradesmodule[nodeindex]);
			}
		}

		/// <summary>
		/// Reapplies all ignored upgrades to ensure they won't be disabled.
		/// </summary>
        void ReapplyIgnoredUpgrades()
        {
            if (string.IsNullOrEmpty(UpgradesToIgnore))
                return;
            var uptign = SplitSingleLineToList(UpgradesToIgnore, ',');
            foreach (string ignoredup in uptign)
			{
                if (PartUpgradeManager.Handler.IsUnlocked(ignoredup) == false)
                    continue;
                PartUpgradeManager.Handler.SetEnabled(ignoredup, true);
                if (ListOfTemporarilyDisabledUpgrades.Contains(ignoredup))
                    ListOfTemporarilyDisabledUpgrades.Remove(ignoredup);
                RegeneratePartUpgrades(ignoredup);
			}
        }

		/// <summary>
		/// Refreshes the Part and all its Modules featuring the upgrade which was modified.
		/// </summary>
		void RegeneratePartUpgrades (string partupgradename)
        {
            var checkem = false;

            // Reapply all upgrades.
			foreach (PartModule mmm in part.Modules)
			{
                if (!mmm.HasUpgrades())
                    continue;
                checkem = false;
				mmm.FindUpgrades(true);
				foreach (ConfigNode cwc in mmm.upgrades)
				{
                    if (cwc.GetValue("name__") == partupgradename && checkem == false)
					{						
                        if (mmm.upgradesApplied.Count > 0)
                        {
                            mmm.ApplyUpgrades(StartState.Editor);
                        }
                        checkem = true;
					}
				}
            }		
        }

		/// <summary>
		/// Generates and spawns the Upgrade Editor UI.
		/// </summary>
		public PopupDialog GenerateUpgradeEditorUI()
		{
            upgrade_editor.Clear();
            upgrade_line.Clear();
			upgrade_editor.Add(new DialogGUIHorizontalLayout(new DialogGUISpace(50), new DialogGUILabel("Upgrade"), new DialogGUISpace(150), new DialogGUILabel("Changes"), new DialogGUISpace(250), new DialogGUILabel("Description")));
			upgrade_editor.Add(new DialogGUISpace(4));


			// create one line with toggle upgrade button, all modules affected and description for every upgrade in the selected part
			for (int cns = 0; cns < allupgrades.Count; cns++)
			{
				
				var affected_modules = new List<string>();
				foreach (PartModule yapm in part.Modules)
				{
					if (yapm.HasUpgrades())
					{
						foreach (ConfigNode ymup in yapm.upgrades)
						{
							if (ymup.GetValue("name__") == allupgrades[cns].GetValue("name__"))
								affected_modules.Add(yapm.GetModuleDisplayName() + " => " + ymup.GetValue("description__"));
						}
					}
				}

				// Using PartUpgradeManager.Handler to access the Upgrade as located in the Tech Tree referred 
				// in the PartModules and find out its in-game name and description.
				var nameforcurrentupgrade = allupgrades[cns].GetValue("name__");
				var currentupgrade = PartUpgradeManager.Handler.GetUpgrade(nameforcurrentupgrade);

				if (affected_modules[0] == " => ")
					affected_modules[0] = "See general description for details";

                var single_upgrade_button = new DialogGUIToggleButton(() => !ListOfTemporarilyDisabledUpgrades.Contains(currentupgrade.name), currentupgrade.title, OnButtonClick_Upgrade(currentupgrade), button_width, button_height); //PartUpgradeManager.Handler.IsEnabled(currentupgrade.name)
				var single_modules_button = new DialogGUIBox(string.Join("\n", affected_modules.ToArray()), 250, button_height);
				var single_desc_button = new DialogGUIBox(currentupgrade.description,340, button_height);

				var h = new DialogGUIHorizontalLayout(true, false, 4, new RectOffset(), TextAnchor.MiddleCenter, new DialogGUIBase[] { single_upgrade_button, single_modules_button, single_desc_button });
				upgrade_line.Add(h);
			}

			scrollList = null;
			scrollList = new DialogGUIBase[upgrade_line.Count + 1];

			scrollList[0] = new DialogGUIContentSizer(ContentSizeFitter.FitMode.Unconstrained, ContentSizeFitter.FitMode.PreferredSize, true);

			for (int i = 0; i < upgrade_line.Count; i++)
				scrollList[i + 1] = upgrade_line[i];
			upgrade_editor.Add(new DialogGUIScrollList(new Vector2(270, 200), false, true,
			                                           new DialogGUIVerticalLayout(10, 100, 4, new RectOffset(6, 24, 10, 10), TextAnchor.UpperLeft, scrollList)
				));
			upgrade_editor.Add(new DialogGUISpace(4));

            upgrade_editor.Add(new DialogGUIHorizontalLayout(new DialogGUIBase[]
            {
				new DialogGUIFlexibleSpace(),
                new DialogGUIButton( "Reset & Close", ResetUnlockedUpgrades),
				new DialogGUIFlexibleSpace(),
                new DialogGUIToggleButton(()=> EnableAllUpgrades, "Toggle All", OnButtonClick_EnableAllUpgrades,-1,30),                
                new DialogGUIFlexibleSpace(),
                new DialogGUIToggleButton(()=> PartUpgradeHandler.AllEnabled, "Always Enable", OnButtonClick_ToggleAllUpgrades,-1,30),
                new DialogGUIFlexibleSpace(),
                new DialogGUIButton("Close", Dismiss)
			}));
			return PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new MultiOptionDialog(
               "Upgrade Editor",
               "", "Upgrade Editor",
               HighLogic.UISkin,
                // window origin is center of rect, position is offset from lower left corner of screen and normalized i.e (0.5, 0.5 is screen center)
                new Rect(0.5f, 0.5f, width, height), upgrade_editor.ToArray()), false, HighLogic.UISkin);
		}
       
        #region  OnButtonClick methods called by the GuiButtons and Toggles
        Callback<bool> OnButtonClick_Upgrade(PartUpgradeHandler.Upgrade thisupgrade)
		{
			return delegate (bool arg1)
            {
                if (arg1 == false)
                {                        
                    PartUpgradeManager.Handler.SetEnabled(thisupgrade.name, false);
                    if (!ListOfTemporarilyDisabledUpgrades.Contains(thisupgrade.name))
                        ListOfTemporarilyDisabledUpgrades.Add(thisupgrade.name);                  
                    //Debug.Log("Upgrade Editor: " + thisupgrade.name + "'s Enable set to:" + PartUpgradeManager.Handler.IsEnabled(thisupgrade.name));
                }

                if (arg1 == true)
                {
                    PartUpgradeManager.Handler.SetEnabled(thisupgrade.name, true);
                    if (ListOfTemporarilyDisabledUpgrades.Contains(thisupgrade.name))
                        ListOfTemporarilyDisabledUpgrades.Remove(thisupgrade.name);
                    //Debug.Log("Upgrade Editor: " + thisupgrade.name + "'s Enable set to:" + PartUpgradeManager.Handler.IsEnabled(thisupgrade.name));
                }
                ReloadPartModulesConfigs();
                ReapplyIgnoredUpgrades();
                RegeneratePartUpgrades(thisupgrade.name);

                //Such double check is necessary.
                foreach (ConfigNode u in allupgrades)
                {
					var checkEnabled = PartUpgradeManager.Handler.IsEnabled(u.GetValue("name__"));
					PartUpgradeManager.Handler.SetEnabled(u.GetValue("name__"), !ListOfTemporarilyDisabledUpgrades.Contains(u.GetValue("name__")));
					if (PartUpgradeManager.Handler.IsEnabled(u.GetValue("name__")) != checkEnabled)
						RegeneratePartUpgrades(u.GetValue("name__"));                   
                }
            };
		}

        // Toggle all upgrades either on or off, without overriding

		void OnButtonClick_EnableAllUpgrades(bool eau)
		{
            EnableAllUpgrades = eau;
            ReloadPartModulesConfigs();
            ReapplyIgnoredUpgrades();
            foreach (ConfigNode it in allupgrades)
            {
                var checkEnabled = PartUpgradeManager.Handler.IsEnabled(it.GetValue("name__"));
                PartUpgradeManager.Handler.SetEnabled(it.GetValue("name__"),eau);
                if (!eau && !ListOfTemporarilyDisabledUpgrades.Contains(it.GetValue("name__")))
                    ListOfTemporarilyDisabledUpgrades.Add(it.GetValue("name__"));
                if (eau && ListOfTemporarilyDisabledUpgrades.Contains(it.GetValue("name__")))
                    ListOfTemporarilyDisabledUpgrades.Remove(it.GetValue("name__"));
                if (PartUpgradeManager.Handler.IsEnabled(it.GetValue("name__")) != checkEnabled)
                    RegeneratePartUpgrades(it.GetValue("name__"));
            }
		}

        // Sets all upgrades on by default and overrides custom settings from Upgrade Editor

        void OnButtonClick_ToggleAllUpgrades(bool tall)
        {
            ToggleAllUpgrades = PartUpgradeHandler.AllEnabled = tall;
            if (ToggleAllUpgrades)
            {                
                ReloadPartModulesConfigs();
                ReapplyIgnoredUpgrades();
				foreach (ConfigNode itz in allupgrades)
				{
					PartUpgradeManager.Handler.SetEnabled(itz.GetValue("name__"), true);
					if (ListOfTemporarilyDisabledUpgrades.Contains(itz.GetValue("name__")))
                        ListOfTemporarilyDisabledUpgrades.Remove(itz.GetValue("name__"));
					RegeneratePartUpgrades(itz.GetValue("name__"));
				}
            }
        }

		#endregion
	}
}