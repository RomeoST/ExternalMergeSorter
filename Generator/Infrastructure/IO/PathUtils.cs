namespace Generator.Infrastructure.IO
{
    public static class PathUtils
    {
        public static string GetPartPath(string pattern, int index)
        {
            return string.Format(pattern, index);
        }
    }
}
