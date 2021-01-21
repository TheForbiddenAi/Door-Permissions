using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Oxide.Core.Plugins;
using UnityEngine;
using Physics = UnityEngine.Physics;

namespace Oxide.Plugins
{
    [Info("Door Permissions", "TheForbiddenAi", "2.0.0"),
     Description("Allows admins to lock doors to specific permissions.")]
    public class DoorPermissions : RustPlugin
    {
        #region Fields

        private readonly DynamicConfigFile _dataFile = Interface.Oxide.DataFileSystem.GetDatafile("DoorPermissions");
        private ConfigData _configData;

        private const BUTTON SelectDoorButton = BUTTON.FIRE_PRIMARY;

        private const string DoorPermPrefix = "doorpermissions.door.";
        private const string LockDoorPerm = "doorpermissions.lockdoor";
        private const string UnlockDoorPerm = "doorpermissions.unlockdoor";
        private const string ActivateDoorPerm = "doorpermissions.activatedoor";
        private const string DeactivateDoorPerm = "doorpermissions.deactivatedoor";
        private const string ViewDoorInfoPerm = "doorpermissions.viewdoorinfo";
        private const string SetDoorNamePerm = "doorpermissions.setdoorname";
        private const string SetTeleportDoorPerm = "doorpermissions.setteleportdoor";
        private const string SetTeleportPointPerm = "doorpermissions.setteleportpoint";
        private const string SetCategoryPerm = "doorpermissions.setcategory";
        private const string SetCategoryNamePerm = "doorpermissions.setcategoryname";
        private const string AddCategoryPermPerm = "doorpermissions.addcategoryperm";
        private const string RemoveCategoryPermPerm = "doorpermissions.removecategoryperm";
        private const string CreateCategoryPerm = "doorpermissions.createcategory";
        private const string DeleteCategoryPerm = "doorpermissions.deletecategory";
        private const string ViewCategoryInfoPerm = "doorpermissions.viewcategoryinfo";
        private const string ViewCategoriesPerm = "doorpermissions.viewcategories";

        private List<string> _zDoorFronts = new List<string>();
        private List<string> _positiveEntrances = new List<string>();

        private enum DoorActions
        {
            Lock,
            Unlock,
            Activate,
            Deactivate,
            ViewInfo,
            SetDoorName,
            SetTeleportDoor,
            CreatingTelePoint,
            SettingDoorCategory
        }

        private string _version;

        #endregion

        #region Config

        private class ConfigData
        {
            // Default values exist in the scenario that someone messes up their config keys, the code will still execute
            [JsonProperty(PropertyName = "Date File Version - DO NOT MODIFY")]
            public string DataFileVersion = "1.0.1";

            [JsonProperty(PropertyName = "Send No Permissons Message When Opening Door Without Required Permissions")]
            public bool SendInsufficientPerms = false;

            [JsonProperty(PropertyName = "Allow Damage To Locked Doors")]
            public bool AllowDamage = false;

            [JsonProperty(PropertyName = "Should Delete Doors On Category Deletion")]
            public bool ShouldDeleteDoors = false;

            public static ConfigData DefaultConfig()
            {
                return new ConfigData()
                {
                    DataFileVersion = "2.0.0",
                    SendInsufficientPerms = true,
                    AllowDamage = false,
                    ShouldDeleteDoors = false
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _configData = Config.ReadObject<ConfigData>();
            if (_configData == null)
            {
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private ConfigData GetDefaultConfig()
        {
            return ConfigData.DefaultConfig();
        }

        private void SaveConfig()
        {
            Config.WriteObject(_configData, true);
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Insufficient Permissions"] =
                    "<color=red>You do not have the required permission to do this action!</color>",
                ["Invalid Syntax"] = "<color=red>Invalid Syntax:</color> <color=white>{0}</color>",
                ["Lock Door Syntax"] = "/lockdoor <permissions...> [note: {0} is implied in the permission]",
                ["Set Door Name Syntax"] = "/setdoorname <name>",
                ["Set Category Syntax"] = "/setcategory <id>",
                ["Set Category Name Syntax"] = "/setcategoryname <id> <name>",
                ["Add Category Perm Syntax"] =
                    "/addcategoryperm <id> <permissions...> [note: {0} is implied in the permission]",
                ["Remove Category Perm Syntax"] =
                    "/removecategoryperm <id> <permissions...> [note: {0} is implied in the permission]",
                ["Create Category Syntax"] =
                    "/createcategory <name> (permissions...) [note: {0} is implied in the permission]",
                ["Delete Category Syntax"] = "/deletecategory <id>",
                ["View Category Info Syntax"] = "/viewcategoryinfo <id>",
                ["Entered Locking Process"] =
                    "<color=#00ffffff>Look at a door and, use your primary attack keybind, to lock it, to cancel run /lockdoor!</color>",
                ["Entered Unlocking Process"] =
                    "<color=#00ffffff>Look at a door and, use your primary attack keybind, to unlock it, to cancel run /unlockdoor!</color>",
                ["Entered Activation Process"] =
                    "<color=#00ffffff>Look at a door and, use your primary attack keybind, to activate it, to cancel run /activatedoor!</color>",
                ["Entered Deactivation Process"] =
                    "<color=#00ffffff>Look at a door and, use your primary attack keybind, to deactivate it, to cancel run /deactivatedoor!</color>",
                ["Entered View Info Process"] =
                    "<color=#00ffffff>Look at a door and, use your primary attack keybind, to view it's information, to cancel run /viewdoorinfo!</color>",
                ["Entered Set Door Name Process"] =
                    "<color=#00ffffff>Look at a door and, use your primary attack keybind, to set it's name, to cancel run /setdoorname!</color>",
                ["Entered Set Tele Door Process"] =
                    "<color=#00ffffff>Look at a door and, use your primary attack keybind," +
                    " to make it a teleportation door, to cancel run /setteleportdoor!</color>",
                ["Entered Set Tele Point Process"] =
                    "<color=#00ffffff>Look at a door and, use your primary attack keybind, " +
                    "to create a teleportation point, to cancel run /setteleportpoint!</color>",
                ["Entered Set Category Process"] =
                    "<color=#00ffffff>Look at a door and, use your primary attack keybind, to set its category, to cancel run /setcategory!</color>",
                ["Door Locked"] = "<i>Door has been locked to the permission(s):</i> <color=green>{0}</color>",
                ["Door Locked New Permissions"] =
                    "<i>Door has been locked to the permission(s):</i> <color=green>{0}</color> <i>(prexisiting permissions still remain)</i>",
                ["Door Unlocked"] =
                    "<i>This door is no longer locked to the following permission(s):</i> <color=green>{0}</color>",
                ["Door Activated"] = "<color=green>This door is now activated!</color>",
                ["Door Deactivated"] =
                    "<color=green>This door is now</color> <color=red>deactivated</color><color=green>!</color>",
                ["Set Door Name"] =
                    "<color=green>Successfully changed the door's name to</color> {0}<color=green>!</color>",
                ["Set Tele Door"] = "<color=green>This door is now a teleportation door!</color>",
                ["Removed Tele Door"] = "<color=green>This door is no longer a teleportation door!</color>",
                ["Set Tele Point"] = "<color=green>Set Teleportation Point at</color> {0}",
                ["Set Category Name"] =
                    "<color=green>Successfully changed category name to</color> {0}<color=green>!</color>",
                ["Category Locked New Permissions"] =
                    "<i>The given category has been locked to the permission(s):</i> <color=green>{0}</color> <i>(prexisiting permissions still remain)</i>",
                ["Category Set"] =
                    "<color=green>Successfully changed the category to the category with an id of</color> {0}<color=green>!</color>",
                ["Category Unlocked"] =
                    "<i>The given category is no longer locked to the following permission(s):</i> <color=green>{0}</color>",
                ["Category Created"] =
                    "<color=green>Successfully created a category with the name of</color> {0}<color=green>!</color>",
                ["Category Deleted"] =
                    "<color=green>Successfully deleted the category with the id of</color> {0}<color=green>!</color>",
                ["Exited Process"] = "<color=green>Successfully exited the previous process!</color>",

                ["Locked To Category Not Door"] =
                    "<color=red>This door is not locked to the permission:</color> <i>{0}<i>, but it's category is! Continuing with remaining permissions.",
                ["View Door Info Format"] =
                    "<color=yellow>Door Name:</color> {0},\n<color=yellow>Category Id:</color> {1}," +
                    "\n<color=yellow>Door Active:</color> {2},\n<color=yellow>Permission List(Does not include category permissions):</color> {3}",
                ["View Category Info Format"] =
                    "<color=yellow>Category Name:</color> {0},\n<color=yellow>Category Id:</color> {1}," +
                    "\n<color=yellow>Permission List(Does not include any door permissions):</color> {2}",
                ["View Categories Format"] = "<color=yellow>Id:</color> {0}, <color=yellow>Name:</color> {1},\n",
                ["Failed To Create Codelock"] =
                    "<color=red>Failed to create code lock entity, please try again.</color>",
                ["Time Ended"] = "<color=yellow>The time period to select a door has ended!</color>",
                ["Not A Door"] =
                    "<color=red>You are either too far from the door or the entity you are looking at is not a door!</color>",
                ["Door Not Locked"] = "<color=red>This door is not currently locked to any permission!</color>",
                ["Door Is Not Locked To That Permission"] =
                    "<color=red>This door is not locked to the permission:</color> <i>{0}</i> " +
                    "<color=red>continuing with remaining permissions!</color>",
                ["Door Already Locked"] =
                    "<color=red>This door is already locked to the permission:</color> {0} </color>continuing with remaining permissions</color>",
                ["Door Already Activated"] = "<color=red>This door is already activated!</color>",
                ["Door Already Deactivated"] = "<color=red>This door is already deactivated!</color>",
                ["Door Already Has Name"] = "<color=red>This door is already has that name!</color>",
                ["Not A Category"] = "<color=red>There is no category with the id</color> {0}<color=red>!</color>",
                ["Not A Number"] = "<color=red>That is not a valid id, ids are only numbers!</color>",
                ["Already Has Category"] =
                    "<color=red>The selected door already has the category with an id of</color> {0}<color=green>!</color>",
                ["Category Already Locked"] =
                    "<color=red>That category is already locked to the permission:</color> {0} </color>continuing with remaining permissions</color>",
                ["Category Already Has Name"] = "<color=red>That category already has that name!</color>",
                ["Can Not Delete Default Category"] = "<color=red>You can not delete the default category!</color>",
                ["Category Is Not Locked To That Permission"] =
                    "<color=red>That category is not locked to the permission:</color> <i>{0}</i> " +
                    "<color=red>continuing with remaining permissions!</color>",
                ["No Categories Found"] = "<color=red>Uh Oh! There are no categories to display.</color>"
            }, this);
        }

        private string GetMessage(string key, BasePlayer player, params object[] args) =>
            string.Format(lang.GetMessage(key, this, player.UserIDString), args);

        #endregion

        #region Initalization

        private void Init()
        {
            _version = Version.ToString();

            if (_configData.AllowDamage)
            {
                Unsubscribe("OnEntityTakeDamage");
            }

            RegisterPermissions(
                LockDoorPerm,
                UnlockDoorPerm,
                ActivateDoorPerm,
                DeactivateDoorPerm,
                ViewDoorInfoPerm,
                SetDoorNamePerm,
                SetTeleportDoorPerm,
                SetTeleportPointPerm,
                SetCategoryPerm,
                SetCategoryNamePerm,
                AddCategoryPermPerm,
                RemoveCategoryPermPerm,
                CreateCategoryPerm,
                DeleteCategoryPerm,
                ViewCategoryInfoPerm,
                ViewCategoriesPerm
            );

            string dataFileVersion = _configData.DataFileVersion;
            
            if (!dataFileVersion.Equals(_version))
            {
                UpdateDataFile(dataFileVersion);
            }
            

            CacheDataObjects();
            InitializeQuaternionLists();
        }

        private void CacheDataObjects()
        {
            Dictionary<string, object> categoryJson = _dataFile["Categories"] as Dictionary<string, object>;
            if (categoryJson == null)
            {
                Puts("Invalid Data File!");
                return;
            }

            foreach (KeyValuePair<string, object> pair in categoryJson)
            {
                string catJson = JsonConvert.SerializeObject(pair.Value);
                CategoryObject category = JsonConvert.DeserializeObject<CategoryObject>(catJson);

                category.Id = int.Parse(pair.Key);
                category.Doors.ForEach(door =>
                {
                    door.Category = category;
                    RegisterPermissions(door.GetAllPermissions().ToArray());
                    DoorObject.AddDoors(door);
                });

                CategoryObject.AddCategory(category);
            }
        }

        private void UpdateDataFile(string oldVersion)
        {
            switch (oldVersion)
            {
                case "1.0.1":
                    int catId = 0;

                    List<DoorObject> doors = new List<DoorObject>();

                    foreach (KeyValuePair<string, object> pair in _dataFile)
                    {
                        DoorObject doorObj = new DoorObject();

                        string[] coords = Regex.Replace(pair.Key, @"[^0-9,.-]", "").Split(',');

                        doorObj.Position = new Vector3(float.Parse(coords[0]), float.Parse(coords[1]),
                            float.Parse(coords[2]));
                        doorObj.CategoryId = catId;
                        doorObj.IsActive = (bool) _dataFile[pair.Key, "active"];

                        List<object> rawPermissions = (List<object>) _dataFile[pair.Key, "permissions"];
                        List<string> permissions = rawPermissions.Select(obj => obj.ToString()).ToList();
                        doorObj.Permissions = permissions;

                        doors.Add(doorObj);
                    }

                    CategoryObject category = new CategoryObject
                    {
                        Id = catId,
                        Doors = doors,
                        Permissions = new List<string>()
                    };

                    _dataFile.Clear();
                    _dataFile["Categories", catId.ToString()] = category;

                    _configData.DataFileVersion = _version;
                    SaveConfig();
                    _dataFile.Save();
                    break;
            }
        }

        private void InitializeQuaternionLists()
        {
            Quaternion xDoorFrontQ = new Quaternion(0, 0.2F, 0, -1F);
            string xDoorFront = xDoorFrontQ.ToString();

            Quaternion zDoorFrontQ = new Quaternion(0, 0.6F, 0, 0.8F);
            Quaternion zDoorFront2Q = new Quaternion(0, 0.8F, 0, -0.6F);

            string zDoorFront = zDoorFrontQ.ToString();
            string zDoorFront2 = zDoorFront2Q.ToString();
            string zDoorFront3 = Quaternion.Inverse(zDoorFrontQ).ToString();
            string zDoorFront4 = Quaternion.Inverse(zDoorFront2Q).ToString();

            _zDoorFronts = new List<string> {zDoorFront, zDoorFront2, zDoorFront3, zDoorFront4};
            _positiveEntrances = new List<string> {xDoorFront, zDoorFront2, zDoorFront4};
        }

        #endregion

        #region Hooks

        private object CanUseLockedEntity(BasePlayer player, BaseLock baseLock)
        {
            BaseEntity parent = baseLock.GetParentEntity();
            if (!(parent is Door)) return null;

            Vector3 doorPosition = parent.transform.position;

            DoorObject doorData = DoorObject.GetDoorByLocation(doorPosition);
            if (doorData == null) return null;

            if (!doorData.IsActive) return null;
            List<string> permList = doorData.GetAllPermissions();

            if (!HasPermissions(player, permList.ToArray()))
            {
                if (_configData.SendInsufficientPerms)
                    player.ChatMessage(GetMessage("Insufficient Permissions", player));
                return null;
            }

            bool shouldUnlock = true;
            if (doorData.IsTeleportationDoor)
            {
                Vector3 playerPosition = player.transform.position;
                Vector3 entrance = doorData.TeleportationEntrance;
                Vector3 exit = doorData.TeleportationExit;

                bool shouldEnter = !IsDoorEntrance((Door) parent, playerPosition);

                player.Teleport(shouldEnter ? entrance : exit);

                shouldUnlock = false;
            }

            GameObjectRef unlockSound = (baseLock as CodeLock)?.effectUnlocked;
            if (unlockSound == null)
            {
                Puts("Unable to retrieve unlock sound for code lock!");
                return shouldUnlock;
            }

            Effect.server.Run(unlockSound.resourcePath, player.transform.position, Vector3.zero);

            return shouldUnlock;
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            BaseEntity baseEntity = info.HitEntity;
            if (!(baseEntity is Door)) return null;

            Vector3 position = baseEntity.transform.position;
            DoorObject doorData = DoorObject.GetDoorByLocation(position);
            if (doorData == null) return null;

            return true;
        }

        private void OnEntityKill(BaseNetworkable entity)
        {
            if (!(entity is Door)) return;

            Vector3 position = entity.transform.position;
            DoorObject doorData = DoorObject.GetDoorByLocation(position);
            if (doorData == null) return;

            doorData.IsActive = false;
            doorData.UpdateDoor(this);
        }

        #endregion

        #region Commands

        [ChatCommand("lockdoor")]
        private void LockDoorCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPermissions(player, LockDoorPerm))
            {
                player.ChatMessage(GetMessage("Insufficient Permissions", player));
                return;
            }

            if (TimerHandler.HasTimer(player) && TimerHandler.GetTimer(player).Action == DoorActions.Lock)
            {
                player.ChatMessage(GetMessage("Exited Process", player));
                TimerHandler.RemoveTimer(player);
                return;
            }

            if (args.Length < 1)
            {
                string syntax = GetMessage("Lock Door Syntax", player, DoorPermPrefix);
                player.ChatMessage(GetMessage("Invalid Syntax", player, syntax));
                return;
            }

            TimerHandler.RemoveTimer(player);
            args = args.Select(str => (DoorPermPrefix + str).ToLower()).ToArray();

            player.ChatMessage(GetMessage("Entered Locking Process", player));

            TimerHandler timerHandler = new TimerHandler(this,
                player,
                DoorActions.Lock,
                0.1f,
                LockDoor(player, args)
            );
        }

        [ChatCommand("unlockdoor")]
        private void UnlockDoorCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPermissions(player, UnlockDoorPerm))
            {
                player.ChatMessage(GetMessage("Insufficient Permissions", player));
                return;
            }

            if (TimerHandler.HasTimer(player) && TimerHandler.GetTimer(player).Action == DoorActions.Unlock)
            {
                player.ChatMessage(GetMessage("Exited Process", player));
                TimerHandler.RemoveTimer(player);
                return;
            }

            TimerHandler.RemoveTimer(player);
            args = args.Select(str => (DoorPermPrefix + str).ToLower()).ToArray();

            player.ChatMessage(GetMessage("Entered Unlocking Process", player));

            TimerHandler timerHandler = new TimerHandler(this,
                player,
                DoorActions.Unlock,
                0.1f,
                UnlockDoor(player, args)
            );
        }

        [ChatCommand("activatedoor")]
        private void ActivateDoorCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPermissions(player, ActivateDoorPerm))
            {
                player.ChatMessage(GetMessage("Insufficient Permissions", player));
                return;
            }

            if (TimerHandler.HasTimer(player) && TimerHandler.GetTimer(player).Action == DoorActions.Activate)
            {
                player.ChatMessage(GetMessage("Exited Process", player));
                TimerHandler.RemoveTimer(player);
                return;
            }

            TimerHandler.RemoveTimer(player);

            player.ChatMessage(GetMessage("Entered Activation Process", player));

            TimerHandler timerHandler = new TimerHandler(this,
                player,
                DoorActions.Activate,
                0.1f,
                ActivateDoor(player)
            );
        }

        [ChatCommand("deactivatedoor")]
        private void DeactivateDoorCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPermissions(player, DeactivateDoorPerm))
            {
                player.ChatMessage(GetMessage("Insufficient Permissions", player));
                return;
            }

            if (TimerHandler.HasTimer(player) && TimerHandler.GetTimer(player).Action == DoorActions.Deactivate)
            {
                player.ChatMessage(GetMessage("Exited Process", player));
                TimerHandler.RemoveTimer(player);
                return;
            }

            TimerHandler.RemoveTimer(player);

            player.ChatMessage(GetMessage("Entered Deactivation Process", player));

            TimerHandler timerHandler = new TimerHandler(this,
                player,
                DoorActions.Deactivate,
                0.1f,
                DeactivateDoor(player)
            );
        }

        [ChatCommand("viewdoorinfo")]
        private void ViewDoorInfoCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPermissions(player, ViewDoorInfoPerm))
            {
                player.ChatMessage(GetMessage("Insufficient Permissions", player));
                return;
            }

            if (TimerHandler.HasTimer(player) && TimerHandler.GetTimer(player).Action == DoorActions.ViewInfo)
            {
                player.ChatMessage(GetMessage("Exited Process", player));
                TimerHandler.RemoveTimer(player);
                return;
            }

            TimerHandler.RemoveTimer(player);

            player.ChatMessage(GetMessage("Entered View Info Process", player));

            TimerHandler timerHandler = new TimerHandler(this,
                player,
                DoorActions.ViewInfo,
                0.1f,
                ViewDoorInfo(player)
            );
        }

        [ChatCommand("setdoorname")]
        private void SetDoorNameCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPermissions(player, LockDoorPerm))
            {
                player.ChatMessage(GetMessage("Insufficient Permissions", player));
                return;
            }

            if (TimerHandler.HasTimer(player) && TimerHandler.GetTimer(player).Action == DoorActions.SetDoorName)
            {
                player.ChatMessage(GetMessage("Exited Process", player));
                TimerHandler.RemoveTimer(player);
                return;
            }

            if (args.Length < 1)
            {
                string syntax = GetMessage("Set Door Name Syntax", player);
                player.ChatMessage(GetMessage("Invalid Syntax", player, syntax));
                return;
            }

            TimerHandler.RemoveTimer(player);
            string newName = string.Join(" ", args);

            player.ChatMessage(GetMessage("Entered Set Door Name Process", player));

            TimerHandler timerHandler = new TimerHandler(this,
                player,
                DoorActions.SetDoorName,
                0.1f,
                SetDoorName(player, newName)
            );
        }

        [ChatCommand("setteleportdoor")]
        private void SetTeleportDoorCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPermissions(player, SetTeleportDoorPerm))
            {
                player.ChatMessage(GetMessage("Insufficient Permissions", player));
                return;
            }

            if (TimerHandler.HasTimer(player) && TimerHandler.GetTimer(player).Action == DoorActions.SetTeleportDoor)
            {
                player.ChatMessage(GetMessage("Exited Process", player));
                TimerHandler.RemoveTimer(player);
                return;
            }

            TimerHandler.RemoveTimer(player);

            player.ChatMessage(GetMessage("Entered Set Tele Door Process", player));

            TimerHandler timerHandler = new TimerHandler(this,
                player,
                DoorActions.SetTeleportDoor,
                0.1f,
                SetTeleDoor(player)
            );
        }

        [ChatCommand("setteleportpoint")]
        private void SetTeleportPointCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPermissions(player, SetTeleportPointPerm))
            {
                player.ChatMessage(GetMessage("Insufficient Permissions", player));
                return;
            }

            if (TimerHandler.HasTimer(player) && TimerHandler.GetTimer(player).Action == DoorActions.CreatingTelePoint)
            {
                player.ChatMessage(GetMessage("Exited Process", player));
                TimerHandler.RemoveTimer(player);
                return;
            }

            TimerHandler.RemoveTimer(player);

            player.ChatMessage(GetMessage("Entered Set Tele Point Process", player));
            Vector3 playerPosition = player.transform.position;

            TimerHandler timerHandler = new TimerHandler(this,
                player,
                DoorActions.CreatingTelePoint,
                0.1f,
                SetTelePoint(player, playerPosition)
            );
        }

        [ChatCommand("setcategory")]
        private void SetCategoryCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPermissions(player, SetCategoryPerm))
            {
                player.ChatMessage(GetMessage("Insufficient Permissions", player));
                return;
            }

            if (TimerHandler.HasTimer(player) &&
                TimerHandler.GetTimer(player).Action == DoorActions.SettingDoorCategory)
            {
                player.ChatMessage(GetMessage("Exited Process", player));
                TimerHandler.RemoveTimer(player);
                return;
            }

            if (args.Length < 1)
            {
                string syntax = GetMessage("Set Category Syntax", player);
                player.ChatMessage(GetMessage("Invalid Syntax", player, syntax));
                return;
            }

            CategoryObject category = TryGetCategory(player, args[0]);
            if (category == null) return;

            TimerHandler.RemoveTimer(player);

            player.ChatMessage(GetMessage("Entered Set Category Process", player));

            TimerHandler timerHandler = new TimerHandler(this,
                player,
                DoorActions.SettingDoorCategory,
                0.1f,
                SetCategory(player, category)
            );
        }

        [ChatCommand("setcategoryname")]
        private void SetCategoryNameCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPermissions(player, SetCategoryNamePerm))
            {
                player.ChatMessage(GetMessage("Insufficient Permissions", player));
                return;
            }

            if (args.Length < 2)
            {
                string syntax = GetMessage("Set Category Name Syntax", player);
                player.ChatMessage(GetMessage("Invalid Syntax", player, syntax));
                return;
            }

            CategoryObject category = TryGetCategory(player, args[0]);
            if (category == null) return;

            List<string> nameArgs = args.ToList();
            nameArgs.Remove(args[0]);

            string newName = string.Join(" ", nameArgs);
            if (category.Name.Equals(newName))
            {
                player.ChatMessage(GetMessage("Category Already Has Name", player));
                return;
            }

            category.Name = newName;
            category.Save(this);
            player.ChatMessage(GetMessage("Set Category Name", player, newName));
        }

        [ChatCommand("addcategoryperm")]
        private void AddCategoryPermCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPermissions(player, AddCategoryPermPerm))
            {
                player.ChatMessage(GetMessage("Insufficient Permissions", player));
                return;
            }

            if (args.Length < 2)
            {
                string syntax = GetMessage("Add Category Perm Syntax", player, DoorPermPrefix);
                player.ChatMessage(GetMessage("Invalid Syntax", player, syntax));
                return;
            }

            CategoryObject category = TryGetCategory(player, args[0]);
            if (category == null) return;

            List<string> permissions = category.Permissions;

            List<string> newPerms = args.ToList();
            newPerms.Remove(args[0]);
            newPerms = newPerms.Select(str => (DoorPermPrefix + str).ToLower()).ToList();

            newPerms.RemoveAll(perm =>
            {
                if (!permissions.Contains(perm)) return false;
                player.ChatMessage(GetMessage("Category Already Locked", player, perm));
                return true;
            });

            if (newPerms.Count == 0) return;

            permissions.AddRange(newPerms);

            UpdateCategoryPermissions(category, permissions.ToArray());
            player.ChatMessage(GetMessage("Category Locked New Permissions", player, string.Join(", ", newPerms)));
        }

        [ChatCommand("removecategoryperm")]
        private void RemoveCategoryPermCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPermissions(player, RemoveCategoryPermPerm))
            {
                player.ChatMessage(GetMessage("Insufficient Permissions", player));
                return;
            }

            if (args.Length < 2)
            {
                string syntax = GetMessage("Remove Category Perm Syntax", player, DoorPermPrefix);
                player.ChatMessage(GetMessage("Invalid Syntax", player, syntax));
                return;
            }

            CategoryObject category = TryGetCategory(player, args[0]);
            if (category == null) return;

            List<string> permissions = category.Permissions;

            List<string> removePerms = args.ToList();
            removePerms.Remove(args[0]);
            removePerms = removePerms.Select(str => (DoorPermPrefix + str).ToLower()).ToList();

            removePerms.RemoveAll(perm =>
            {
                if (permissions.Contains(perm)) return false;
                player.ChatMessage(GetMessage("Category Is Not Locked To That Permission", player, perm));
                return true;
            });

            if (removePerms.Count == 0) return;

            permissions.RemoveAll(perm => removePerms.Contains((perm)));

            UpdateCategoryPermissions(category, permissions.ToArray());
            player.ChatMessage(GetMessage("Category Unlocked", player, string.Join(", ", removePerms)));
        }

        [ChatCommand("createcategory")]
        private void CreateCategoryCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPermissions(player, CreateCategoryPerm))
            {
                player.ChatMessage(GetMessage("Insufficient Permissions", player));
                return;
            }

            if (args.Length < 1)
            {
                string syntax = GetMessage("Create Category Syntax", player, DoorPermPrefix);
                player.ChatMessage(GetMessage("Invalid Syntax", player, syntax));
                return;
            }

            string catName = args[0];

            List<string> catPerms = args.ToList();
            catPerms.Remove(catName);
            catPerms = catPerms.Select(str => (DoorPermPrefix + str).ToLower()).ToList();

            CategoryObject newCategory = new CategoryObject
            {
                Id = CategoryObject.GetNewCategoryId(),
                Name = catName,
                Permissions = catPerms
            };
            RegisterPermissions(catPerms.ToArray());

            // Save method adds category to cache automatically
            newCategory.Save(this);

            player.ChatMessage(GetMessage("Category Created", player, catName));
        }

        [ChatCommand("deletecategory")]
        private void DeleteCategoryCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPermissions(player, DeleteCategoryPerm))
            {
                player.ChatMessage(GetMessage("Insufficient Permissions", player));
                return;
            }

            if (args.Length < 1)
            {
                string syntax = GetMessage("Delete Category Syntax", player);
                player.ChatMessage(GetMessage("Invalid Syntax", player, syntax));
                return;
            }

            CategoryObject category = TryGetCategory(player, args[0]);
            if (category == null) return;

            if (category.Id == 0)
            {
                player.ChatMessage(GetMessage("Can Not Delete Default Category", player));
                return;
            }

            if (!_configData.ShouldDeleteDoors)
            {
                // 0 is default category
                category.MoveAllDoors(this, 0);
            }

            CategoryObject.DeleteCategory(this, category.Id);
            player.ChatMessage(GetMessage("Category Deleted", player, args[0]));
        }

        [ChatCommand("viewcategoryinfo")]
        private void ViewCategoryInfoCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPermissions(player, DeleteCategoryPerm))
            {
                player.ChatMessage(GetMessage("Insufficient Permissions", player));
                return;
            }

            if (args.Length < 1)
            {
                string syntax = GetMessage("View Category Info Syntax", player);
                player.ChatMessage(GetMessage("Invalid Syntax", player, syntax));
                return;
            }

            CategoryObject category = TryGetCategory(player, args[0]);
            if (category == null) return;

            string catName = category.Name;
            string catPerms = string.Join(", ", category.Permissions);

            player.ChatMessage(GetMessage("View Category Info Format", player, catName, args[0], catPerms));
        }

        [ChatCommand("viewcategories")]
        private void ViewCategoriesCommand(BasePlayer player, string command, string[] args)
        {
            if (!HasPermissions(player, ViewCategoriesPerm))
            {
                player.ChatMessage(GetMessage("Insufficient Permissions", player));
                return;
            }

            StringBuilder stringBuilder = new StringBuilder();
            CategoryObject.GetCategories().ForEach(category =>
            {
                int catId = category.Id;
                string catName = category.Name;
                string format = GetMessage("View Categories Format", player, catId, catName);
                stringBuilder.Append(format);
            });

            player.ChatMessage(stringBuilder.Length == 0
                ? GetMessage("No Categories Found", player)
                : stringBuilder.ToString());
        }

        #endregion

        #region Door Logic

        private Action LockDoor(BasePlayer player, string[] args)
        {
            return () =>
            {
                if (!player.serverInput.IsDown(SelectDoorButton)) return;

                Door door = RetrieveDoorEntity(player);
                if (door == null) return;

                Vector3 doorPosition = door.transform.position;
                List<string> newPerms = args.ToList();

                DoorObject doorData = DoorObject.GetDoorByLocation(doorPosition);
                if (doorData != null)
                {
                    List<string> currentPermissionList = doorData.GetAllPermissions();

                    newPerms.RemoveAll(perm =>
                    {
                        if (!currentPermissionList.Contains(perm)) return false;

                        player.ChatMessage(GetMessage("Door Already Locked", player, perm));
                        return true;
                    });

                    if (newPerms.Count == 0) return;
                    currentPermissionList.AddRange(newPerms);
                    UpdateDoorPermissions(doorData, currentPermissionList.ToArray());

                    string joinedPerms = string.Join(", ", newPerms);
                    player.ChatMessage(GetMessage("Door Locked New Permissions", player, joinedPerms));

                    TimerHandler.RemoveTimer(player);
                    return;
                }

                doorData = new DoorObject
                {
                    Name = "Unknown",
                    Position = doorPosition,
                    Permissions = newPerms,
                    Category = CategoryObject.GetCategoryById(0)
                };

                RegisterPermissions(args);

                if (!PutLockOnDoor(door, player))
                {
                    TimerHandler.RemoveTimer(player);
                    return;
                }

                DoorObject.AddDoors(doorData);
                doorData.UpdateDoor(this);

                string permStr = string.Join(", ", newPerms);
                player.ChatMessage(GetMessage("Door Locked", player, permStr));

                TimerHandler.RemoveTimer(player);
            };
        }

        private Action UnlockDoor(BasePlayer player, string[] args)
        {
            return () =>
            {
                if (!player.serverInput.IsDown(SelectDoorButton)) return;

                Door door = RetrieveDoorEntity(player);
                if (door == null) return;

                Vector3 doorPosition = door.transform.position;
                List<string> unlockPerms = args.ToList();

                DoorObject doorData = DoorObject.GetDoorByLocation(doorPosition);
                if (doorData == null)
                {
                    player.ChatMessage(GetMessage("Door Not Locked", player));
                    TimerHandler.RemoveTimer(player);
                    return;
                }

                List<string> newDoorPerms = doorData.Permissions;

                // New list to stop aliasing
                List<string> preserveDoorPerms = new List<string>(doorData.Permissions);

                unlockPerms.RemoveAll(perm =>
                {
                    if (newDoorPerms.Contains(perm)) return false;

                    string msg = doorData.Category.Permissions.Contains(perm)
                        ? "Locked To Category Not Door"
                        : "Door Is Not Locked To That Permission";

                    player.ChatMessage(GetMessage(msg, player, perm));
                    return true;
                });

                newDoorPerms.RemoveAll(perm => unlockPerms.Contains(perm));

                bool killEntity = unlockPerms.Count == 0 ||
                                  (newDoorPerms.Count == 0 && doorData.Category.Permissions.Count == 0);
                if (killEntity)
                {
                    RemoveLockOnDoor(door);
                    doorData.DeleteDoor(this);
                }
                else
                {
                    UpdateDoorPermissions(doorData, newDoorPerms.ToArray());
                }

                List<string> permList = killEntity ? preserveDoorPerms : unlockPerms;
                player.ChatMessage(GetMessage("Door Unlocked", player, string.Join(", ", permList)));
                TimerHandler.RemoveTimer(player);
            };
        }

        private Action ActivateDoor(BasePlayer player)
        {
            return () =>
            {
                if (!player.serverInput.IsDown(SelectDoorButton)) return;

                Door door = RetrieveDoorEntity(player);
                if (door == null) return;

                Vector3 doorPosition = door.transform.position;

                DoorObject doorData = DoorObject.GetDoorByLocation(doorPosition);
                if (doorData == null)
                {
                    player.ChatMessage(GetMessage("Door Not Locked", player));
                    TimerHandler.RemoveTimer(player);
                    return;
                }

                if (doorData.IsActive)
                {
                    player.ChatMessage(GetMessage("Door Already Activated", player));
                    TimerHandler.RemoveTimer(player);
                    return;
                }

                if (!PutLockOnDoor(door, player))
                {
                    TimerHandler.RemoveTimer(player);
                    return;
                }

                player.ChatMessage(GetMessage("Door Activated", player));

                doorData.IsActive = true;
                doorData.UpdateDoor(this);
                TimerHandler.RemoveTimer(player);
            };
        }

        private Action DeactivateDoor(BasePlayer player)
        {
            return () =>
            {
                if (!player.serverInput.IsDown(SelectDoorButton)) return;

                Door door = RetrieveDoorEntity(player);
                if (door == null) return;

                Vector3 doorPosition = door.transform.position;

                DoorObject doorData = DoorObject.GetDoorByLocation(doorPosition);
                if (doorData == null)
                {
                    player.ChatMessage(GetMessage("Door Not Locked", player));
                    TimerHandler.RemoveTimer(player);
                    return;
                }

                if (!doorData.IsActive)
                {
                    player.ChatMessage(GetMessage("Door Already Deactivated", player));
                    TimerHandler.RemoveTimer(player);
                    return;
                }

                RemoveLockOnDoor(door);

                player.ChatMessage(GetMessage("Door Deactivated", player));

                doorData.IsActive = false;
                doorData.UpdateDoor(this);
                TimerHandler.RemoveTimer(player);
            };
        }

        private Action ViewDoorInfo(BasePlayer player)
        {
            return () =>
            {
                if (!player.serverInput.IsDown(SelectDoorButton)) return;

                Door door = RetrieveDoorEntity(player);
                if (door == null) return;

                Vector3 doorPosition = door.transform.position;

                DoorObject doorData = DoorObject.GetDoorByLocation(doorPosition);
                if (doorData == null)
                {
                    player.ChatMessage(GetMessage("Door Not Locked", player));
                    TimerHandler.RemoveTimer(player);
                    return;
                }

                string name = doorData.Name;
                int catId = doorData.CategoryId;
                bool active = doorData.IsActive;

                List<string> permList = doorData.Permissions;
                string formattedPerms = string.Join(", ", permList);

                string message = GetMessage("View Door Info Format", player, name, catId, active, formattedPerms);
                player.ChatMessage(message);

                TimerHandler.RemoveTimer(player);
            };
        }

        private Action SetDoorName(BasePlayer player, string newName)
        {
            return () =>
            {
                if (!player.serverInput.IsDown(SelectDoorButton)) return;

                Door door = RetrieveDoorEntity(player);
                if (door == null) return;

                Vector3 doorPosition = door.transform.position;

                DoorObject doorData = DoorObject.GetDoorByLocation(doorPosition);
                if (doorData == null)
                {
                    player.ChatMessage(GetMessage("Door Not Locked", player));
                    TimerHandler.RemoveTimer(player);
                    return;
                }

                if (doorData.Name.Equals(newName))
                {
                    player.ChatMessage(GetMessage("Door Already Has Name", player));
                    TimerHandler.RemoveTimer(player);
                    return;
                }

                doorData.Name = newName;
                doorData.UpdateDoor(this);

                player.ChatMessage(GetMessage("Set Door Name", player, newName));
                TimerHandler.RemoveTimer(player);
            };
        }

        private Action SetTeleDoor(BasePlayer player)
        {
            return () =>
            {
                if (!player.serverInput.IsDown(SelectDoorButton)) return;

                Door door = RetrieveDoorEntity(player);
                if (door == null) return;

                Vector3 doorPosition = door.transform.position;

                DoorObject doorData = DoorObject.GetDoorByLocation(doorPosition);
                if (doorData == null)
                {
                    player.ChatMessage(GetMessage("Door Not Locked", player));
                    TimerHandler.RemoveTimer(player);
                    return;
                }

                bool setState = !doorData.IsTeleportationDoor;
                doorData.IsTeleportationDoor = setState;
                doorData.UpdateDoor(this);

                if (setState) door.SetFlag(BaseEntity.Flags.Open, false);
                player.ChatMessage(GetMessage(setState ? "Set Tele Door" : "Removed Tele Door", player));

                TimerHandler.RemoveTimer(player);
            };
        }

        private Action SetTelePoint(BasePlayer player, Vector3 playerPosition)
        {
            return () =>
            {
                if (!player.serverInput.IsDown(SelectDoorButton)) return;

                Door door = RetrieveDoorEntity(player);
                if (door == null) return;

                Vector3 doorPosition = door.transform.position;

                DoorObject doorData = DoorObject.GetDoorByLocation(doorPosition);
                if (doorData == null)
                {
                    player.ChatMessage(GetMessage("Door Not Locked", player));
                    TimerHandler.RemoveTimer(player);
                    return;
                }

                bool isEntrance = IsDoorEntrance(door, playerPosition);

                if (isEntrance)
                {
                    doorData.TeleportationEntrance = playerPosition;
                }
                else
                {
                    doorData.TeleportationExit = playerPosition;
                }

                doorData.UpdateDoor(this);
                player.ChatMessage(GetMessage("Set Tele Point", player, playerPosition.ToString()));

                TimerHandler.RemoveTimer(player);
            };
        }

        private Action SetCategory(BasePlayer player, CategoryObject category)
        {
            return () =>
            {
                if (!player.serverInput.IsDown(SelectDoorButton)) return;

                Door door = RetrieveDoorEntity(player);
                if (door == null) return;

                Vector3 doorPosition = door.transform.position;

                DoorObject doorData = DoorObject.GetDoorByLocation(doorPosition);
                if (doorData == null)
                {
                    player.ChatMessage(GetMessage("Door Not Locked", player));
                    TimerHandler.RemoveTimer(player);
                    return;
                }

                int id = category.Id;
                if (doorData.CategoryId == id)
                {
                    player.ChatMessage(GetMessage("Already Has Category", player, id));
                    TimerHandler.RemoveTimer(player);
                    return;
                }

                string msg = doorData.SetCategory(this, id) ? "Category Set" : "Not A Category";
                player.ChatMessage(GetMessage(msg, player, id));

                TimerHandler.RemoveTimer(player);
            };
        }

        #endregion

        #region Classes

        public class CategoryObject
        {
            private static readonly List<CategoryObject> CategoryCache = new List<CategoryObject>();

            [JsonIgnore] public int Id { get; set; } = -1;
            public string Name { get; set; } = "Unknown";
            public List<DoorObject> Doors { get; set; } = new List<DoorObject>();
            public List<string> Permissions { get; set; } = new List<string>();

            public void MoveAllDoors(DoorPermissions plugin, int id)
            {
                CategoryObject category = GetCategoryById(id);
                if (category == null) return;

                DoorObject.RemoveDoorsFromCache(Doors.ToArray());
                Doors.ForEach(door =>
                {
                    door.CategoryId = category.Id;
                    door.Category = category;
                });

                category.Doors.AddRange(Doors);

                Doors.Clear();
                DoorObject.AddDoorsToCache(plugin, category.Doors.ToArray());

                SaveCategories(plugin, this, category);
            }

            public static int GetNewCategoryId()
            {
                return CategoryCache.Count;
            }

            public static bool CategoryExists(int id)
            {
                return GetCategoryById(id) != null;
            }

            public static CategoryObject GetCategoryById(int id)
            {
                return CategoryCache.FirstOrDefault(cat => cat.Id == id);
            }

            public static List<CategoryObject> GetCategories()
            {
                return CategoryCache;
            }

            public static void AddCategory(params CategoryObject[] categoryArray)
            {
                CategoryCache.AddRange(categoryArray);
            }

            public static void DeleteCategory(DoorPermissions plugin, int id)
            {
                CategoryObject category = GetCategoryById(id);
                if (category == null) return;
                CategoryCache.Remove(category);

                List<CategoryObject> newCache = new List<CategoryObject>();
                SortedDictionary<int, CategoryObject> saveDictionary = new SortedDictionary<int, CategoryObject>();
                CategoryCache.ForEach(listCat =>
                {
                    int listId = listCat.Id;
                    listId -= listId > id ? 1 : 0;

                    listCat.Id = listId;

                    saveDictionary[listId] = listCat;
                    newCache.Add(listCat);
                });

                CategoryCache.Clear();
                CategoryCache.AddRange(newCache);

                plugin._dataFile["Categories"] = saveDictionary;
                plugin._dataFile.Save();
                return;
            }

            public void Save(DoorPermissions plugin)
            {
                SaveCategories(plugin, this);
            }

            private static void SaveCategories(DoorPermissions plugin, params CategoryObject[] categoryArray)
            {
                foreach (CategoryObject category in categoryArray)
                {
                    CategoryCache.RemoveAll(cat => cat.Name.Equals(category.Name));
                    int catId = category.Id;

                    plugin._dataFile["Categories", catId.ToString()] = category;
                }

                CategoryCache.AddRange(categoryArray);

                plugin._dataFile.Save();
            }
        }

        public class DoorObject
        {
            private static readonly List<DoorObject> DoorCache = new List<DoorObject>();

            public int CategoryId { get; set; }
            public string Name { get; set; } = "Unknown";
            public Vector3 Position { get; set; } = Vector3.zero;
            public bool IsActive { get; set; } = true;
            public bool IsTeleportationDoor { get; set; }
            public Vector3 TeleportationEntrance { get; set; } = Vector3.zero;
            public Vector3 TeleportationExit { get; set; } = Vector3.zero;
            public List<string> Permissions { get; set; } = new List<string>();

            [JsonIgnore] public CategoryObject Category { get; set; } = new CategoryObject();

            public List<string> GetAllPermissions()
            {
                var allPerms = new List<string>(Permissions);
                allPerms.AddRange(Category.Permissions);

                return allPerms;
            }

            public static List<DoorObject> GetDoorByName(string name)
            {
                return DoorCache.FindAll(door => door.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            }

            public static DoorObject GetDoorByLocation(Vector3 position)
            {
                /*
                 * Comparing using strings because I had an issue previously where 2 vectors which were equal were
                 * returning false when not comparing using strings. Assuming it is because of some precision error,
                 * happened with both == and .Equals
                 */
                return DoorCache.Find(door => door.Position.ToString().Equals(position.ToString()));
            }

            public static void AddDoors(params DoorObject[] doorArray)
            {
                foreach (DoorObject door in doorArray)
                {
                    List<DoorObject> doorList = door.Category.Doors;

                    if (!doorList.Contains(door)) door.Category.Doors.Add(door);
                    DoorCache.Add(door);
                }
            }

            public static void AddDoorsToCache(DoorPermissions plugin, params DoorObject[] doorArray)
            {
                // Makes sure there are no duplicates
                RemoveDoorsFromCache(doorArray);
                DoorCache.AddRange(doorArray);
            }

            public static void RemoveDoorsFromCache(params DoorObject[] doorArray)
            {
                foreach (DoorObject door in doorArray)
                {
                    DoorCache.RemoveAll(otherDoor =>
                    {
                        string doorPosition = door.Position.ToString();
                        string otherDoorPosition = otherDoor.Position.ToString();

                        return doorPosition.Equals(otherDoorPosition);
                    });
                }
            }

            public void UpdateDoor(DoorPermissions plugin)
            {
                Category.Doors.RemoveAll(door => door.Position.ToString().Equals(Position.ToString()));
                DoorCache.RemoveAll(door => door.Position.ToString().Equals(Position.ToString()));

                Category.Doors.Add(this);
                DoorCache.Add(this);

                Category.Save(plugin);
            }

            public void DeleteDoor(DoorPermissions plugin)
            {
                // This is done case any of the fields change in door compared to using Remove(this)
                Category.Doors.RemoveAll(door => door.Position.ToString().Equals(Position.ToString()));
                DoorCache.RemoveAll(door => door.Position.ToString().Equals(Position.ToString()));
                Category.Save(plugin);
            }

            public bool SetCategory(DoorPermissions plugin, int id)
            {
                CategoryObject newCategory = CategoryObject.GetCategoryById(id);
                if (newCategory == null) return false;

                Category.Doors.RemoveAll(door => door.Position.ToString().Equals(Position.ToString()));
                Category.Save(plugin);

                CategoryId = id;
                Category = newCategory;
                UpdateDoor(plugin);
                return true;
            }
        }

        private class TimerHandler
        {
            private static readonly Dictionary<string, TimerHandler> ActiveTimers =
                new Dictionary<string, TimerHandler>();


            private readonly Timer _handledTimer;
            private readonly Timer _expireTimer;
            public readonly DoorActions Action;

            public TimerHandler(DoorPermissions plugin, BasePlayer player, DoorActions action, float interval,
                Action callback, long expireAfterSeconds = 30)
            {
                Action = action;
                _handledTimer = plugin.timer.Every(interval, callback);
                _expireTimer = plugin.timer.In(expireAfterSeconds, () =>
                {
                    player.ChatMessage(plugin.GetMessage("Time Ended", player));
                    _handledTimer.Destroy();
                });

                AddTimer(player, this);
            }

            public void CancelTimer()
            {
                _handledTimer.Destroy();
                _expireTimer.Destroy();
            }

            public static bool HasTimer(BasePlayer player)
            {
                return ActiveTimers.ContainsKey(player.UserIDString);
            }

            public static TimerHandler GetTimer(BasePlayer player)
            {
                return ActiveTimers[player.UserIDString];
            }

            private static void AddTimer(BasePlayer player, TimerHandler handler)
            {
                ActiveTimers[player.UserIDString] = handler;
            }

            public static void RemoveTimer(BasePlayer player)
            {
                if (!HasTimer(player)) return;

                GetTimer(player).CancelTimer();
                ActiveTimers.Remove(player.UserIDString);
            }
        }

        #endregion

        #region Utlity Methods

        private bool HasPermissions(BasePlayer player, params string[] permissions)
        {
            string userId = player.UserIDString;
            return permissions.Any(permName => permission.UserHasPermission(userId, permName));
        }

        public void UpdateDoorPermissions(DoorObject door, params string[] perms)
        {
            door.Permissions = perms.ToList();
            RegisterPermissions(perms);
            door.UpdateDoor(this);
        }

        public void UpdateCategoryPermissions(CategoryObject category, params string[] perms)
        {
            category.Permissions = perms.ToList();
            RegisterPermissions(perms);
            category.Save(this);
        }

        public void RegisterPermissions(params string[] permissions)
        {
            List<string> permList = permissions.ToList();
            permList.RemoveAll(perm => permission.PermissionExists(perm));
            permList.ForEach(perm => permission.RegisterPermission(perm, this));
        }

        private bool PutLockOnDoor(Door door, BasePlayer player)
        {
            if (door.GetSlot(BaseEntity.Slot.Lock) != null)
            {
                RemoveLockOnDoor(door);
            }

            CodeLock codeLockEntity = GameManager.server.CreateEntity("assets/prefabs/locks/keypad/lock.code.prefab",
                Vector3.zero, Quaternion.identity) as CodeLock;
            if (codeLockEntity == null)
            {
                player.ChatMessage(GetMessage("Failed To Create Codelock", player));
                return false;
            }

            codeLockEntity.SetParent(door, door.GetSlotAnchorName(BaseEntity.Slot.Lock));
            codeLockEntity.Spawn();

            door.SetSlot(BaseEntity.Slot.Lock, codeLockEntity);

            CodeLock codeLock = (CodeLock) door.GetSlot(BaseEntity.Slot.Lock);
            codeLock.SetFlag(BaseEntity.Flags.Locked, true);
            return true;
        }

        private void RemoveLockOnDoor(Door door)
        {
            if (door.GetSlot(BaseEntity.Slot.Lock) == null) return;

            CodeLock codeLock = (CodeLock) door.GetSlot(BaseEntity.Slot.Lock);
            codeLock.GetEntity().Kill();
            door.SetSlot(BaseEntity.Slot.Lock, null);
        }

        // Credit to Wulf
        private static BaseEntity FindObject(Ray ray, float distance)
        {
            RaycastHit hit;
            return Physics.Raycast(ray, out hit, distance) ? hit.GetEntity() : null;
        }

        private Door RetrieveDoorEntity(BasePlayer player)
        {
            Ray ray = new Ray(player.eyes.position, player.eyes.HeadForward());

            BaseEntity entity = FindObject(ray, 1.5f);
            if (entity != null && entity is Door) return entity as Door;

            player.ChatMessage(GetMessage("Not A Door", player));
            return null;
        }

        // This does not account for if both points are behind/in front of the door
        private bool IsDoorEntrance(Door door, Vector3 playerPosition)
        {
            Vector3 doorPosition = door.transform.position;
            string doorRotStr = door.GetNetworkRotation().ToString();

            bool isZ = _zDoorFronts.Contains(doorRotStr);
            float coordDifference = isZ ? doorPosition.z - playerPosition.z : doorPosition.x - playerPosition.x;

            return _positiveEntrances.Contains(doorRotStr) && coordDifference > 0 ||
                   !_positiveEntrances.Contains(doorRotStr) && coordDifference < 0;
        }

        private CategoryObject TryGetCategory(BasePlayer player, string strId)
        {
            int id;
            bool isNumber = int.TryParse(strId, out id);
            if (!isNumber)
            {
                player.ChatMessage(GetMessage("Not A Number", player));
                return null;
            }

            if (CategoryObject.CategoryExists(id)) return CategoryObject.GetCategoryById(id);

            player.ChatMessage(GetMessage("Not A Category", player, id));
            return null;
        }

        #endregion
    }
}