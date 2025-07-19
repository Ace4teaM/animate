using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Animate
{
    internal static class Extensions
    {

        internal static T FindAncestor<T>(this DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T)
                {
                    return (T)current;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        internal static T? GetItemAt<T>(this ListView listView, Point point) where T : class
        {
            var element = listView.InputHitTest(point) as DependencyObject;
            var listViewItem = FindAncestor<ListViewItem>(element);
            return listViewItem != null ? listView.ItemContainerGenerator.ItemFromContainer(listViewItem) as T : null;
        }
    }
}
