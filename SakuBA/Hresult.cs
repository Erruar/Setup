namespace SakuBA
{
    internal class Hresult
    {
        public static bool Succeeded(int status) => status >= 0;
    }
}
