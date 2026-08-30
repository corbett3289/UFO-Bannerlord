using System;
using System.Reflection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.ScreenSystem;
namespace UFO.Extension
{
    public static class Reflection
    {
        private static readonly BindingFlags DeclaredBindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        public static bool TryGetViewModel<T>(this ScreenBase screen, out T viewModel) where T : class
        {
            viewModel = null;
            if (screen == null)
            {
                return false;
            }

            FieldInfo field = FindField(screen.GetType(), "_dataSource");
            if (field?.GetValue(screen) is T namedViewModel)
            {
                viewModel = namedViewModel;
                return true;
            }

            for (Type type = screen.GetType(); type != null; type = type.BaseType)
            {
                foreach (FieldInfo candidate in type.GetFields(DeclaredBindingFlags))
                {
                    if (typeof(T).IsAssignableFrom(candidate.FieldType) && candidate.GetValue(screen) is T typedViewModel)
                    {
                        viewModel = typedViewModel;
                        return true;
                    }
                }
            }

            return false;
        }

        public static T GetViewModel<T>(this ScreenBase screen) where T : class
        {
            screen.TryGetViewModel(out T viewModel);
            return viewModel;
        }

        public static void InitializeTroopLists(this PartyVM partyVM)
        {
            MethodInfo method = FindMethod(partyVM?.GetType(), "InitializeTroopLists");
            method?.Invoke(partyVM, new object[0]);
        }

        public static SPItemVM GetSelectedItem(this SPInventoryVM inventoryVM)
        {
            FieldInfo field = FindField(inventoryVM?.GetType(), "_selectedItem");
            return field?.GetValue(inventoryVM) as SPItemVM;
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, DeclaredBindingFlags);
                if (field != null)
                {
                    return field;
                }
            }

            return null;
        }

        private static MethodInfo FindMethod(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            {
                MethodInfo method = type.GetMethod(name, DeclaredBindingFlags, null, Type.EmptyTypes, null);
                if (method != null)
                {
                    return method;
                }
            }

            return null;
        }
    }
}
