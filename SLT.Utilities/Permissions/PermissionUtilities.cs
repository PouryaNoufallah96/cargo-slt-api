namespace Utilities.Permissions
{
    public static class PermissionUtilities
    {
        public static Dictionary<string, string> GetAllPermissions()
        {
            return Permissions.PermissionsList.ToDictionary(q => q.Title, q => q.Description);
        }

        public static Dictionary<string, Dictionary<string, string>> GetClassifiedPermissions()
        {
            Dictionary<string, Dictionary<string, string>> result = [];

            foreach (var category in Permissions.AllRoles)
                result[category] = Permissions.PermissionsList
                    .Where(p => p.Roles.Contains(category.ToLower()))
                    .ToDictionary(q => q.Title, q => q.Description);

            return result;
        }

        public static List<string> GetCodeOfPermissionsByTheirTitle(IEnumerable<string> permissionsTitle)
        {
            return Permissions.PermissionsList.Where(p => permissionsTitle?.Contains(p.Title) ?? false).Select(p => p.Code).ToList();
        }

        public static List<string> GetTitleOfPermissionsByTheirCode(IEnumerable<string> permissionsCode)
        {
            return Permissions.PermissionsList.Where(p => permissionsCode?.Contains(p.Code) ?? false).Select(p => p.Title).ToList();
        }
    }

}
