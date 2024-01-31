using System.Text;

namespace RenderPipelines.SourceGenertor
{
    internal sealed class FormatStringBuilder
    {
        public FormatStringBuilder() { }

        public void Append(string text)
        {
            stringBuilder.Append(text);
        }
        public void AppendSpace()
        {
            stringBuilder.Append(' ', spaces);
        }

        public void AppendLine()
        {
            stringBuilder.AppendLine();
        }
        public void AppendLine(string text)
        {
            stringBuilder.Append(' ', spaces);
            stringBuilder.AppendLine(text);
        }
        public void Open(string bracket)
        {
            AppendLine(bracket);
            spaces += 4;
        }

        public void Close(string bracket)
        {
            spaces -= 4;
            AppendLine(bracket);
        }

        public void Clear()
        {
            stringBuilder.Clear();
        }

        public StringBuilder stringBuilder = new StringBuilder();

        public int spaces = 0;

        public override string ToString()
        {
            return stringBuilder.ToString();
        }
    }
}
