using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AzurLaneDex.Models;

namespace AzurLaneDex.Selectors
{
    public class AcquisitionMethodEditTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ConstructionTemplate { get; set; }
        public DataTemplate DropTemplate { get; set; }
        public DataTemplate ExchangeTemplate { get; set; }
        public DataTemplate ResearchTemplate { get; set; }
        public DataTemplate OtherTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is AcquisitionMethod method)
            {
                return method.Type switch
                {
                    AcquisitionMethodType.Drop => DropTemplate,
                    AcquisitionMethodType.Exchange => ExchangeTemplate,
                    AcquisitionMethodType.Research => ResearchTemplate,
                    AcquisitionMethodType.Other => OtherTemplate,
                    _ => ConstructionTemplate
                };
            }
            return ConstructionTemplate;
        }
    }
}