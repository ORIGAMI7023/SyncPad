using System.Collections.Specialized;
using SyncPad.Client.ViewModels;

namespace SyncPad.Client.Controls;

/// <summary>
/// 自由网格布局的文件视图，支持根据 Position(X,Y) 定位文件
/// </summary>
public class FileGridView : ScrollView
{
    private readonly AbsoluteLayout _container;
    private readonly Grid _grid;
    private const int ColumnCount = 4; // 固定4列
    private const int CellWidth = 120;
    private const int CellHeight = 120;

    // 拖放指示器
    private Frame? _dropIndicator;

    // 跟踪文件ID到视图的映射，用于动画
    private readonly Dictionary<int, View> _itemViews = new();

    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource),
        typeof(IEnumerable<SelectableFileItem>),
        typeof(FileGridView),
        null,
        propertyChanged: OnItemsSourceChanged);

    public static readonly BindableProperty ItemTemplateProperty = BindableProperty.Create(
        nameof(ItemTemplate),
        typeof(DataTemplate),
        typeof(FileGridView),
        null);

    public IEnumerable<SelectableFileItem>? ItemsSource
    {
        get => (IEnumerable<SelectableFileItem>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public FileGridView()
    {
        _grid = new Grid
        {
            ColumnSpacing = 2,
            RowSpacing = 2,
            Padding = 5
        };

        // 创建列定义（固定4列）
        for (int i = 0; i < ColumnCount; i++)
        {
            _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = CellWidth });
        }

        // 使用 AbsoluteLayout 包装 Grid，以便在上面覆盖指示器
        _container = new AbsoluteLayout();
        AbsoluteLayout.SetLayoutBounds(_grid, new Rect(0, 0, 1, 1));
        AbsoluteLayout.SetLayoutFlags(_grid, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.All);
        _container.Children.Add(_grid);

        // 创建拖放指示器
        _dropIndicator = new Frame
        {
            WidthRequest = CellWidth,
            HeightRequest = CellHeight,
            BackgroundColor = Color.FromRgba(30, 144, 255, 0.3), // DodgerBlue 半透明
            BorderColor = Color.FromRgb(30, 144, 255),
            CornerRadius = 4,
            Padding = 0,
            IsVisible = false,
            InputTransparent = true // 不拦截鼠标事件
        };

        Content = _container;
    }

    private static void OnItemsSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (FileGridView)bindable;

        // 取消旧集合的监听
        if (oldValue is INotifyCollectionChanged oldCollection)
        {
            oldCollection.CollectionChanged -= control.OnCollectionChanged;
        }

        // 监听新集合
        if (newValue is INotifyCollectionChanged newCollection)
        {
            newCollection.CollectionChanged += control.OnCollectionChanged;
        }

        control.RebuildGrid();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // 对于替换操作，检查是否只是位置变化，如果是则使用动画
        if (e.Action == NotifyCollectionChangedAction.Replace &&
            e.NewItems != null && e.OldItems != null &&
            e.NewItems.Count == 1 && e.OldItems.Count == 1)
        {
            var oldItem = e.OldItems[0] as SelectableFileItem;
            var newItem = e.NewItems[0] as SelectableFileItem;

            if (oldItem != null && newItem != null && oldItem.Id == newItem.Id)
            {
                // 同一文件，检查位置是否变化
                if (oldItem.PositionX != newItem.PositionX || oldItem.PositionY != newItem.PositionY)
                {
                    // 只是位置变化，使用动画更新
                    AnimateItemPosition(newItem, e.NewStartingIndex);
                    return;
                }
            }
        }

        // 其他情况，完全重建
        RebuildGrid();
    }

    private void RebuildGrid()
    {
        if (ItemsSource == null || ItemTemplate == null)
        {
            _grid.Children.Clear();
            _itemViews.Clear();
            return;
        }

        _grid.Children.Clear();
        _grid.RowDefinitions.Clear();
        _itemViews.Clear();

        // 计算需要的最大行数
        int maxRow = 0;
        foreach (var item in ItemsSource)
        {
            if (item.PositionY > maxRow)
                maxRow = item.PositionY;
        }

        // 至少创建足够多的行（最少10行，确保有足够的拖放空间）
        int minRows = 10;
        int rowCount = Math.Max(maxRow + 1, minRows);

        // 创建行定义
        for (int i = 0; i < rowCount; i++)
        {
            _grid.RowDefinitions.Add(new RowDefinition { Height = CellHeight });
        }

        System.Diagnostics.Debug.WriteLine($"[FileGridView] RebuildGrid: {ItemsSource.Count()} 个文件, maxRow={maxRow}, 创建 {rowCount} 行");
        foreach (var item in ItemsSource)
        {
            System.Diagnostics.Debug.WriteLine($"[FileGridView] 文件: {item.FileName} at ({item.PositionX},{item.PositionY})");
        }

        // 添加文件到对应的网格位置
        foreach (var item in ItemsSource)
        {
            // 验证位置有效性
            if (item.PositionX < 0 || item.PositionX >= ColumnCount)
                continue;
            if (item.PositionY < 0)
                continue;

            // 创建视图
            var view = (View)ItemTemplate.CreateContent();
            view.BindingContext = item;

            // 设置到指定的网格位置
            Grid.SetColumn(view, item.PositionX);
            Grid.SetRow(view, item.PositionY);

            _grid.Children.Add(view);

            // 跟踪视图
            _itemViews[item.Id] = view;
        }
    }

    /// <summary>
    /// 使用动画更新文件位置
    /// </summary>
    private async void AnimateItemPosition(SelectableFileItem item, int itemIndex)
    {
        // 查找对应的视图
        if (!_itemViews.TryGetValue(item.Id, out var view))
        {
            // 视图不存在，回退到完全重建
            RebuildGrid();
            return;
        }

        // 确保有足够的行
        int requiredRows = Math.Max(item.PositionY + 1, 10);
        while (_grid.RowDefinitions.Count < requiredRows)
        {
            _grid.RowDefinitions.Add(new RowDefinition { Height = CellHeight });
        }

        // 获取当前位置
        int oldColumn = Grid.GetColumn(view);
        int oldRow = Grid.GetRow(view);

        System.Diagnostics.Debug.WriteLine($"[FileGridView] 动画移动: {item.FileName} 从 ({oldColumn},{oldRow}) 到 ({item.PositionX},{item.PositionY})");

        // 如果位置没变，不需要动画
        if (oldColumn == item.PositionX && oldRow == item.PositionY)
        {
            return;
        }

        // 计算移动距离
        double deltaX = (item.PositionX - oldColumn) * (CellWidth + _grid.ColumnSpacing);
        double deltaY = (item.PositionY - oldRow) * (CellHeight + _grid.RowSpacing);

        // 先更新 Grid 位置（这样布局引擎知道新位置）
        Grid.SetColumn(view, item.PositionX);
        Grid.SetRow(view, item.PositionY);

        // 使用 TranslationX/Y 偏移到旧位置
        view.TranslationX = -deltaX;
        view.TranslationY = -deltaY;

        // 动画回到新位置（Translation 归零）
        await view.TranslateTo(0, 0, 250, Easing.CubicOut);
    }

    /// <summary>
    /// 显示拖放指示器（吸附到网格位置）
    /// </summary>
    public void ShowDropIndicator(double x, double y)
    {
        if (_dropIndicator == null)
            return;

        // 计算网格位置
        int column = (int)(x / CellWidth);
        int row = (int)(y / CellHeight);

        // 限制列范围
        if (column < 0) column = 0;
        if (column >= ColumnCount) column = ColumnCount - 1;
        if (row < 0) row = 0;

        // 计算指示器位置（吸附到网格，包括 Padding 和 Spacing）
        double indicatorX = _grid.Padding.Left + column * (CellWidth + _grid.ColumnSpacing);
        double indicatorY = _grid.Padding.Top + row * (CellHeight + _grid.RowSpacing);

        // 如果指示器还未添加到容器，添加它
        if (!_container.Children.Contains(_dropIndicator))
        {
            _container.Children.Add(_dropIndicator);
        }

        // 更新位置（使用 AbsoluteLayout）
        AbsoluteLayout.SetLayoutBounds(_dropIndicator, new Rect(indicatorX, indicatorY, CellWidth, CellHeight));
        AbsoluteLayout.SetLayoutFlags(_dropIndicator, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.None);
        _dropIndicator.IsVisible = true;

        System.Diagnostics.Debug.WriteLine($"[FileGridView] ShowDropIndicator: ({x:F1}, {y:F1}) -> Grid({column}, {row}) -> Position({indicatorX:F1}, {indicatorY:F1})");
    }

    /// <summary>
    /// 隐藏拖放指示器
    /// </summary>
    public void HideDropIndicator()
    {
        if (_dropIndicator != null)
        {
            _dropIndicator.IsVisible = false;
        }
    }
}
