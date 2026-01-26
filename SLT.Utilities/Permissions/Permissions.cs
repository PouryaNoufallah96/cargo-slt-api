namespace Utilities.Permissions
{
    public static class Permissions
    {
        //User Permissions
        public const string CreateUser = "U1A#";
        public const string EditUser = "U2B!";
        public const string GetAllUsers = "U3C@";
        public const string ArchiveUser = "U4D$";
        public const string BanUser = "U5E%";
        public const string DeleteUser = "U6F^";

        public static readonly List<PermissionMeta> PermissionsList =
        [
                // User Permissions
            new PermissionMeta(CreateUser, nameof(CreateUser), "ایجاد کاربر جدید", ["ceo","hr"]),
            new PermissionMeta(EditUser, nameof(EditUser), "ویرایش اطلاعات کاربر موجود", ["ceo","hr"]),
            new PermissionMeta(GetAllUsers, nameof(GetAllUsers), "مشاهده لیست تمام کاربران", ["ceo","hr"]),
            new PermissionMeta(ArchiveUser, nameof(ArchiveUser), "بایگانی کردن حساب کاربری", ["ceo","hr"]),
            new PermissionMeta(BanUser, nameof(BanUser), "مسدودسازی حساب کاربری", ["ceo","hr"]),
            new PermissionMeta(DeleteUser, nameof(DeleteUser), "حذف یک کاربر", ["ceo","hr"]),
        ];
        public static readonly IEnumerable<string> AllRoles = ["CEO", "CFO", "UnitManager", "ProjectManager", "ContractsManager", "HR"];
    }
    public record PermissionMeta(string Code, string Title, string Description, IEnumerable<string> Roles);
}
