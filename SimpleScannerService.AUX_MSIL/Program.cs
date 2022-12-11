using Saraff.Twain;
using Saraff.Twain.Aux;

internal class Program
{

    [STAThread]
    private static void Main(string[] args)
    {
        using (var _twain32 = new Twain32())
        {
            _twain32.IsTwain2Enable = true;
            _twain32.ShowUI = false;
            _twain32.OpenDSM();

            TwainExternalProcess.Handler(_twain32);
        }
    }
}