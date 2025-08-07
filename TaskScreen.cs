using Engine;
using GameEntitySystem;
using System.Linq;
using System.Xml.Linq;

namespace Game
{
    public class TaskScreen : Screen
    {
        private SubsystemTasks m_subsystemTasks;
        private ListPanelWidget m_tasksList;
        private BevelledButtonWidget m_closeButton;
        private ComponentPlayer m_player;
        private Project project;
        private Screen m_previousScreen;

        public TaskScreen()
        {
            XElement node = ContentManager.Get<XElement>("Screens/TaskScreen");
            LoadContents(this, node);

            m_tasksList = Children.Find<ListPanelWidget>("TasksList");
            m_closeButton = Children.Find<BevelledButtonWidget>("CloseButton");
            m_tasksList.ItemWidgetFactory = CreateTaskItemWidget;
        }

        public override void Enter(object[] parameters)
        {
            base.Enter(parameters);
            m_subsystemTasks = GameManager.Project.FindSubsystem<SubsystemTasks>(true);
            m_player = GameManager.Project.FindSubsystem<SubsystemPlayers>(true).ComponentPlayers.FirstOrDefault();
            m_previousScreen = ScreensManager.PreviousScreen;

            var tasks = m_subsystemTasks.GetAllTasks();
            Log.Information($"加载到 {tasks.Count} 个任务");

            UpdateTaskList();
        }

        public override void Update()
        {
            base.Update();

            if (m_closeButton.IsClicked)
            {
                ScreensManager.SwitchScreen(m_previousScreen);
            }

            if (m_player != null)
            {
                foreach (var task in m_subsystemTasks.GetAllTasks())
                {
                    var claimButton = m_tasksList.Children
                        .OfType<ContainerWidget>()
                        .Select(w => w.Children.Find<BevelledButtonWidget>($"Claim_{task.Id}"))
                        .FirstOrDefault(b => b != null && b.IsClicked);

                    if (claimButton != null)
                    {
                        if (m_subsystemTasks.ClaimReward(task.Id, m_player))
                        {
                            UpdateTaskList();
                        }
                    }
                }
            }
        }

        private void UpdateTaskList()
        {
            Log.Information("开始更新任务列表");

            m_tasksList.Children.Clear();
            Log.Information("已清空任务列表");

            var sortedTasks = m_subsystemTasks.GetAllTasks()
                .OrderBy(t => t.Status)
                .ThenBy(t => t.Id);

            int taskCount = 0;
            foreach (var task in sortedTasks)
            {
                Log.Information($"处理任务: {task.Name} (ID: {task.Id}, 状态: {task.Status})");
                var widget = CreateTaskItemWidget(task);
                m_tasksList.Children.Add(widget);
                taskCount++;
            }

            Log.Information($"添加了 {taskCount} 个任务项到列表");
            m_tasksList.ArrangeOverride();
            Log.Information("任务列表排列完成");
        }

        private Widget CreateTaskItemWidget(object item)
        {
            var task = (Task)item;
            Log.Information($"创建任务项: {task.Name}"); 

            XElement node = ContentManager.Get<XElement>("Widgets/TaskItem");
            var widget = (ContainerWidget)LoadWidget(this, node, null);

            var nameLabel = widget.Children.Find<LabelWidget>("Name");
            if (nameLabel != null)
                nameLabel.Text = task.Name;

            var descriptionLabel = widget.Children.Find<LabelWidget>("Description");
            if (descriptionLabel != null)
                descriptionLabel.Text = task.Description;

            var rewardLabel = widget.Children.Find<LabelWidget>("Reward");
            if (rewardLabel != null)
                rewardLabel.Text = $"奖励: {task.Reward}";

            if (task.Requirements != null && task.Requirements.Count > 0)
            {
                string progressText = task.Requirements.Select(r =>
                    $"{r.Target}: {r.CurrentAmount}/{r.RequiredAmount}").Aggregate((a, b) => $"{a} | {b}");
                var progressLabel = widget.Children.Find<LabelWidget>("Progress");
                if (progressLabel != null)
                    progressLabel.Text = progressText;
            }

            var claimButton = widget.Children.Find<BevelledButtonWidget>("ClaimButton");
            if (claimButton != null)
            {
                claimButton.Name = $"Claim_{task.Id}";
                claimButton.IsVisible = task.Status == TaskStatus.Completed;
            }

            var background = widget.Children.Find<RectangleWidget>("Background");
            if (background != null)
            {
                switch (task.Status)
                {
                    case TaskStatus.Completed:
                        background.FillColor = new Color(0, 100, 0, 80);
                        break;
                    case TaskStatus.Claimed:
                        background.FillColor = new Color(80, 80, 80, 80);
                        foreach (var label in widget.Children.OfType<LabelWidget>())
                            label.Color = new Color(160, 160, 160);
                        break;
                    case TaskStatus.InProgress:
                        background.FillColor = new Color(0, 0, 100, 80);
                        break;
                }
            }

            return widget;
        }
    }
}