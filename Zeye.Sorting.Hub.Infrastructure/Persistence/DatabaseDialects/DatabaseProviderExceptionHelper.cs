namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects {

    internal static class DatabaseProviderExceptionHelper {
        /// <summary>
        /// 尝试从异常链中提取数据库提供器错误码。
        /// </summary>
        public static bool TryGetProviderErrorNumber(Exception exception, out int errorNumber) {
            for (Exception? current = exception; current is not null; current = current.InnerException) {
                var numberProperty = current.GetType().GetProperty("Number");
                if (numberProperty?.PropertyType == typeof(int) && numberProperty.GetValue(current) is int number) {
                    errorNumber = number;
                    return true;
                }
            }

            errorNumber = 0;
            return false;
        }
    }
}
