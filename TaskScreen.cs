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
        private ButtonWidget m_closeButton;
        private ComponentPlayer m_player;
        private Project project;
        private Screen m_previousScreen;

        public TaskScreen()
        {
            XElement node = ContentManager.Get<XElement>("Screens/TaskScreen");
            LoadContents(this, node);

            m_tasksList = Children.Find<ListPanelWidget>("TasksList");
            m_closeButton = Children.Find<ButtonWidget>("CloseButton");
            m_tasksList.ItemWidgetFactory = CreateTaskItemWidget;
            project = Children.Find<Project>();
        }

        public override void Enter(object[] parameters)
        {
            base.Enter(parameters);
            m_subsystemTasks = project.FindSubsystem<SubsystemTasks>(true);
            m_player = project.FindSubsystem<SubsystemPlayers>(true).ComponentPlayers.FirstOrDefault();
            m_previousScreen = ScreensManager.PreviousScreen;
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
                        .Select(w => w.Children.Find<ButtonWidget>($"Claim_{task.Id}"))
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
            m_tasksList.Items.Clear();
            var sortedTasks = m_subsystemTasks.GetAllTasks()
                .OrderBy(t => t.Status)
                .ThenBy(t => t.Id);
            foreach (var task in sortedTasks)
            {
                m_tasksList.Items.Add(task);
            }
            m_tasksList.ArrangeOverride();
        }

        private Widget CreateTaskItemWidget(object item)
        {
            var task = (Task)item;
            XElement node = ContentManager.Get<XElement>("Widgets/TaskItem");
            var widget = (ContainerWidget)LoadWidget(this, node, null);

            widget.Children.Find<LabelWidget>("Name").Text = task.Name;
            widget.Children.Find<LabelWidget>("Description").Text = task.Description;
            widget.Children.Find<LabelWidget>("Reward").Text = $"奖励: {task.Reward}";

            string progressText = task.Requirements.Select(r =>
                $"{r.Target}: {r.CurrentAmount}/{r.RequiredAmount}").Aggregate((a, b) => $"{a} | {b}");
            widget.Children.Find<LabelWidget>("Progress").Text = progressText;

            var claimButton = widget.Children.Find<ButtonWidget>("ClaimButton");
            claimButton.Name = $"Claim_{task.Id}";
            claimButton.IsVisible = task.Status == TaskStatus.Completed;

            var background = widget.Children.Find<RectangleWidget>("Background");
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
            return widget;
        }
    }
}