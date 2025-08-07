using System.Collections.Generic;
using Engine;
using GameEntitySystem;

namespace Game
{
    public enum TaskStatus
    {
        NotStarted,
        InProgress,
        Completed,
        Claimed
    }

    public enum RequirementType
    {
        Collect,   // 收集物品
        Kill,      // 击杀生物
        Build,     // 建造方块
        Reach      // 到达位置
    }

    public class TaskRequirement
    {
        public RequirementType Type { get; set; }
        public string Target { get; set; }  // 目标名称（物品/生物/方块ID）
        public int RequiredAmount { get; set; }
        public int CurrentAmount { get; set; }

        public bool IsCompleted => CurrentAmount >= RequiredAmount;
    }

    public class Task
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Reward { get; set; }  // 奖励格式："物品IDx数量,物品IDx数量"
        public TaskStatus Status { get; set; }
        public List<TaskRequirement> Requirements { get; } = new List<TaskRequirement>();

        public bool AreRequirementsMet()
        {
            foreach (var req in Requirements)
            {
                if (!req.IsCompleted)
                    return false;
            }
            return true;
        }
    }
}