using AzurLaneDex.Helpers;
using AzurLaneDex.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AzurLaneDex.Views.Controls
{
    public sealed partial class AcquireEntryControl : UserControl
    {
        private static readonly ObservableCollection<TagDefinition> _availableTags =
            new ObservableCollection<TagDefinition>(TagLibrary.GetAllTags());

        public event RoutedEventHandler DeleteRequested;

        public AcquireEntryControl()
        {
            this.InitializeComponent();
            TagComboBox.ItemsSource = _availableTags;
            this.DataContextChanged += AcquireEntryControl_DataContextChanged;
        }

        private void AcquireEntryControl_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (args.NewValue is AcquireEntry entry)
            {
                // 同步 ComboBox 选中值
                if (TagComboBox.SelectedValue as string != entry.Tag)
                    TagComboBox.SelectedValue = entry.Tag;

                // 根据当前 Tag 刷新输入面板
                RefreshInputPanel(entry);
            }
        }

        private void OnTagChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TagComboBox.SelectedItem is TagDefinition tagDef && DataContext is AcquireEntry entry)
            {
                // 更新 entry.Tag
                entry.Tag = tagDef.Tag;
                // 刷新输入面板
                RefreshInputPanel(entry);
            }
        }

        private void RefreshInputPanel(AcquireEntry entry)
        {
            // 根据 Tag 找到定义
            var tagDef = _availableTags.FirstOrDefault(t => t.Tag == entry.Tag);
            if (tagDef == null) return;

            // 判断是否为自定义文本类型（注意使用正确的标签值）
            bool isCustomText = entry.Tag == "gear_custom" || entry.Tag == "acquire_custom";
            if (isCustomText)
            {
                // 显示自定义文本编辑框，隐藏参数面板
                ParamsPanel.Visibility = Visibility.Collapsed;
                CustomTextTextBox.Visibility = Visibility.Visible;

                // 加载当前语言的自定义文本
                string currentLang = LocalizationHelper.CurrentLanguage;
                CustomTextTextBox.Text = entry.CustomText?.GetValueOrDefault(currentLang) ?? "";
                CustomTextTextBox.TextChanged -= OnCustomTextChanged;
                CustomTextTextBox.TextChanged += OnCustomTextChanged;
            }
            else
            {
                // 显示参数面板，隐藏自定义文本框
                ParamsPanel.Visibility = Visibility.Visible;
                CustomTextTextBox.Visibility = Visibility.Collapsed;

                // 生成参数输入框
                GenerateParamFields(tagDef, entry);
            }
        }

        private void GenerateParamFields(TagDefinition tagDef, AcquireEntry entry)
        {
            ParamsPanel.Children.Clear();

            for (int i = 0; i < tagDef.ParamCount; i++)
            {
                var paramBox = new TextBox
                {
                    PlaceholderText = $"参数{i + 1}",
                    Width = 200,   // 增加宽度，避免显示不全
                    Margin = new Thickness(0, 0, 8, 0)
                };

                // 确保 Parameters 列表有足够长度，并填充已有值
                while (entry.Parameters.Count <= i)
                    entry.Parameters.Add("");
                paramBox.Text = entry.Parameters[i] ?? "";

                int index = i; // 捕获局部变量
                paramBox.TextChanged += (s, _) =>
                {
                    while (entry.Parameters.Count <= index)
                        entry.Parameters.Add("");
                    entry.Parameters[index] = paramBox.Text;
                };

                ParamsPanel.Children.Add(paramBox);
            }
        }

        private void OnCustomTextChanged(object sender, TextChangedEventArgs e)
        {
            if (DataContext is AcquireEntry entry && entry.Tag == "gear_custom")
            {
                string currentLang = LocalizationHelper.CurrentLanguage;
                if (entry.CustomText == null)
                    entry.CustomText = new LocalizedString();
                entry.CustomText[currentLang] = CustomTextTextBox.Text;
            }
        }

        private void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            DeleteRequested?.Invoke(this, e);
        }
    }
}