using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
    public class TaskModLoader : ModLoader
    {
        private SubsystemTasks m_subsystemTasks;

        public override void __ModInitialize()
        {
            ModsManager.RegisterHook("OnPlayerSpawned", this);
            ModsManager.RegisterHook("OnMinerPlace", this);
            ModsManager.RegisterHook("ProcessAttackment", this);
            ModsManager.RegisterHook("OnPickableCollected", this);
            ModsManager.RegisterHook("OnMinerHit", this);
            ModsManager.RegisterHook("OnProjectLoaded", this);
        }
        public override void OnProjectLoaded(Project project)
        {
            m_subsystemTasks = project.FindSubsystem<SubsystemTasks>(true);
        }
        public override void OnMinerHit(ComponentMiner miner, ComponentBody componentBody, Vector3 hitPoint, Vector3 hitDirection, ref float attackPower, ref float playerProbability, ref float creatureProbability, out bool Hitted)
        {
            Hitted = false;

            Log.Error("OnMinerHit");
            ScreensManager.SwitchScreen(new TaskScreen());
        }
        public override bool OnPlayerSpawned(PlayerData.SpawnMode spawnMode, ComponentPlayer player, Vector3 position)
        {
            // 初始化新手任务
            if (m_subsystemTasks != null)
            {
                var firstTask = m_subsystemTasks.GetAllTasks().FirstOrDefault(t => t.Id == 1);
                if (firstTask != null && firstTask.Status == TaskStatus.NotStarted)
                {
                    player.ComponentGui.DisplaySmallMessage(
                        "新任务可用",
                        Color.White,
                        true,
                        true);
                }
            }
            return false;
        }

        public override void OnMinerPlace(ComponentMiner miner, TerrainRaycastResult raycastResult, int x, int y, int z, int value, out bool placed)
        {
            placed = false; // 不阻止原行为
            if (m_subsystemTasks != null)
            {
                int blockId = Terrain.ExtractContents(value);
                m_subsystemTasks.UpdateTaskProgress(RequirementType.Build, blockId.ToString(), 1);
            }
        }

        public override void ProcessAttackment(Attackment attackment)
        {
            if (m_subsystemTasks != null)
            {
                if (attackment.Target is Entity targetEntity)
                {
                    var targetBody = targetEntity.FindComponent<ComponentBody>();

                    if (attackment.Attacker is Entity attackerEntity)
                    {
                        var attackerMiner = attackerEntity.FindComponent<ComponentMiner>();

                        if (attackerMiner != null && attackerMiner.Entity.FindComponent<ComponentPlayer>() != null)
                        {
                            var targetCreature = targetBody?.Entity.FindComponent<ComponentCreature>();
                            if (targetCreature != null && targetCreature.ComponentHealth.Health <= 0)
                            {
                                string creatureName = targetCreature.Entity.ValuesDictionary.DatabaseObject.Name;
                                m_subsystemTasks.UpdateTaskProgress(RequirementType.Kill, creatureName, 1);
                            }
                        }
                    }
                }
            }
        }
    }
    public class SubsystemTasks : Subsystem
    {
        private List<Task> m_allTasks = new List<Task>();
        private Dictionary<int, Task> m_tasksById = new Dictionary<int, Task>();
        private SubsystemPlayers m_subsystemPlayers;
        private SubsystemBlockBehaviors m_subsystemBlockBehaviors;
        private SubsystemPickables m_subsystemPickables;

        public override void Load(ValuesDictionary valuesDictionary)
        {
            base.Load(valuesDictionary);
            m_subsystemPlayers = Project.FindSubsystem<SubsystemPlayers>(true);
            m_subsystemBlockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(true);
            m_subsystemPickables = Project.FindSubsystem<SubsystemPickables>(true);
            Log.Error("加载任务配置开始");
            LoadTasksFromXml();
            LoadTaskProgress(valuesDictionary);
        }

        private void LoadTasksFromXml()
        {
            try
            {
                XElement tasksElement = ContentManager.Get<XElement>("Data/Tasks");
                foreach (XElement taskElement in tasksElement.Elements("Task"))
                {
                    var task = new Task
                    {
                        Id = int.Parse(taskElement.Attribute("Id").Value),
                        Name = taskElement.Attribute("Name").Value,
                        Description = taskElement.Attribute("Description").Value,
                        Reward = taskElement.Attribute("Reward").Value,
                        Status = (TaskStatus)Enum.Parse(typeof(TaskStatus),
                            taskElement.Attribute("Status")?.Value ?? "NotStarted")
                    };

                    foreach (XElement reqElement in taskElement.Elements("Requirement"))
                    {
                        task.Requirements.Add(new TaskRequirement
                        {
                            Type = (RequirementType)Enum.Parse(typeof(RequirementType), reqElement.Attribute("Type").Value),
                            Target = reqElement.Attribute("Target").Value,
                            RequiredAmount = int.Parse(reqElement.Attribute("Amount").Value)
                        });
                    }

                    m_allTasks.Add(task);
                    m_tasksById[task.Id] = task;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"加载任务配置失败: {ex.Message}");
            }
        }

        private void LoadTaskProgress(ValuesDictionary valuesDictionary)
        {
            foreach (var task in m_allTasks)
            {
                task.Status = (TaskStatus)valuesDictionary.GetValue(
                    $"Task_{task.Id}_Status", (int)task.Status);

                for (int i = 0; i < task.Requirements.Count; i++)
                {
                    task.Requirements[i].CurrentAmount = valuesDictionary.GetValue(
                        $"Task_{task.Id}_Req_{i}", 0);
                }
            }
        }

        public override void Save(ValuesDictionary valuesDictionary)
        {
            base.Save(valuesDictionary);
            foreach (var task in m_allTasks)
            {
                valuesDictionary.SetValue($"Task_{task.Id}_Status", (int)task.Status);

                for (int i = 0; i < task.Requirements.Count; i++)
                {
                    valuesDictionary.SetValue($"Task_{task.Id}_Req_{i}",
                        task.Requirements[i].CurrentAmount);
                }
            }
        }
        // 更新任务进度
        public void UpdateTaskProgress(RequirementType type, string target, int amount = 1)
        {
            foreach (var task in m_allTasks.Where(t => t.Status != TaskStatus.Completed && t.Status != TaskStatus.Claimed))
            {
                var requirement = task.Requirements.FirstOrDefault(r =>
                    r.Type == type && r.Target.Equals(target, StringComparison.OrdinalIgnoreCase));

                if (requirement != null && !requirement.IsCompleted)
                {
                    requirement.CurrentAmount = Math.Min(
                        requirement.CurrentAmount + amount,
                        requirement.RequiredAmount);

                    if (task.AreRequirementsMet())
                    {
                        task.Status = TaskStatus.Completed;
                        ShowTaskCompletedMessage(task);
                    }
                    else if (task.Status == TaskStatus.NotStarted)
                    {
                        task.Status = TaskStatus.InProgress;
                    }
                }
            }
        }
        // 显示任务完成消息
        private void ShowTaskCompletedMessage(Task task)
        {
            if (m_subsystemPlayers != null)
            {
                foreach (var player in m_subsystemPlayers.ComponentPlayers)
                {
                    player.ComponentGui.DisplaySmallMessage(
                        $"任务完成: {task.Name}",
                        Color.Green,
                        true,
                        true);
                }
            }
        }
        // 领取奖励
        public bool ClaimReward(int taskId, ComponentPlayer player)
        {
            if (m_tasksById.TryGetValue(taskId, out var task) &&
                task.Status == TaskStatus.Completed)
            {
                // 检查玩家是否有足够的背包空间
                if (player?.ComponentMiner?.Inventory != null)
                {
                    ComponentInventoryBase inventory = (ComponentInventoryBase)player.ComponentMiner.Inventory;

                    // 解析奖励
                    string[] rewards = task.Reward.Split(',');

                    // 首先检查是否有足够的空间存放所有奖励物品
                    foreach (string reward in rewards)
                    {
                        string[] parts = reward.Trim().Split('x');
                        if (parts.Length == 2 &&
                            int.TryParse(parts[0], out int itemId) &&
                            int.TryParse(parts[1], out int count))
                        {
                            // 检查是否有足够空间
                            if (!HasInventorySpace(inventory, itemId, count))
                            {
                                // 没有足够空间，显示消息并返回false
                                player.ComponentGui.DisplaySmallMessage(
                                    "背包空间不足，无法领取奖励",
                                    Color.Red,
                                    true,
                                    true);
                                return false;
                            }
                        }
                    }

                    // 如果有足够的空间，添加所有物品
                    foreach (string reward in rewards)
                    {
                        string[] parts = reward.Trim().Split('x');
                        if (parts.Length == 2 &&
                            int.TryParse(parts[0], out int itemId) &&
                            int.TryParse(parts[1], out int count))
                        {
                            // 查找空位并添加物品
                            bool itemAdded = false;
                            for (int slotIndex = 0; slotIndex < inventory.SlotsCount; slotIndex++)
                            {
                                if (inventory.GetSlotValue(slotIndex) == 0) // 空槽位
                                {
                                    // 使用完整value参数，包含物品ID和其他可能的数据
                                    int value = Terrain.MakeBlockValue(itemId);
                                    inventory.AddSlotItems(slotIndex, value, count);
                                    itemAdded = true;
                                    break;
                                }
                            }

                            // 如果没有找到空位（理论上不应该发生，因为我们已经检查过了）
                            if (!itemAdded)
                            {
                                player.ComponentGui.DisplaySmallMessage(
                                    "领取奖励时发生错误",
                                    Color.Red,
                                    true,
                                    true);
                                return false;
                            }
                        }
                    }

                    task.Status = TaskStatus.Claimed;
                    return true;
                }
            }
            return false;
        }
        // 辅助方法：检查背包是否有足够空间
        private bool HasInventorySpace(ComponentInventoryBase inventory, int itemId, int count)
        {
            int remainingCount = count;

            // 首先检查现有相同物品的槽位是否可以合并
            for (int slotIndex = 0; slotIndex < inventory.SlotsCount; slotIndex++)
            {
                int slotItemId = inventory.GetSlotValue(slotIndex);
                int slotItemCount = inventory.GetSlotCount(slotIndex);

                if (slotItemId == itemId)
                {
                    // 使用Terrain.MakeBlockValue创建一个值来获取最大堆叠数
                    int blockValue = Terrain.MakeBlockValue(slotItemId);
                    int maxStackSize = inventory.GetSlotCapacity(slotIndex, blockValue);
                    int spaceAvailable = maxStackSize - slotItemCount;
                    if (spaceAvailable > 0)
                    {
                        remainingCount -= Math.Min(remainingCount, spaceAvailable);
                        if (remainingCount <= 0)
                            return true;
                    }
                }
            }

            // 然后检查空槽位
            for (int slotIndex = 0; slotIndex < inventory.SlotsCount; slotIndex++)
            {
                if (inventory.GetSlotValue(slotIndex) == 0) // 空槽位
                {
                    int blockValue = Terrain.MakeBlockValue(itemId);
                    int maxStackSize = inventory.GetSlotCapacity(slotIndex, blockValue);

                    // 计算这个槽位能放多少物品
                    int canAdd = Math.Min(remainingCount, maxStackSize);
                    remainingCount -= canAdd;
                    if (remainingCount <= 0)
                        return true;
                }
            }

            return remainingCount <= 0;
        }

        public List<Task> GetAllTasks() => new List<Task>(m_allTasks);
    }
}