using System.Collections.Generic;
using System.Text;

namespace Coocoo3D.ResourceWrap;

public class ShaderReader
{
    public ShaderReader(string source)
    {
        foreach (char c in source)
        {
            SourceInput(c);
        }
        SourceInput('\n');
    }

    void SourceInput(char c)
    {
        switch (stateCode)
        {
            case StateCode.Init:
                switch (c)
                {
                    case '#':
                        stateCode = StateCode.Pre0;
                        break;
                    case '/':
                        beforeComment = stateCode;
                        stateCode = StateCode.Comment0;
                        break;
                }
                break;
            case StateCode.Pre0:
                switch (c)
                {
                    case ' ':
                    case '\t':
                        AddToken();
                        break;
                    case '\r':
                    case '\n':
                        AddToken();
                        PreAnalyse();
                        stateCode = StateCode.Init;
                        break;
                    case '/':
                        AddToken();
                        beforeComment = stateCode;
                        stateCode = StateCode.Comment0;
                        break;
                    case '\\':
                        beforeEscape = stateCode;
                        stateCode = StateCode.Escape0;
                        break;
                    default:
                        buffer.Append(c);
                        break;
                }
                break;
            case StateCode.Comment0:
                switch (c)
                {
                    case '/':
                        stateCode = StateCode.Comment1;
                        break;
                    case '*':
                        stateCode = StateCode.Comment2;
                        break;
                    default:
                        stateCode = beforeComment;
                        break;
                }
                break;
            case StateCode.Comment1:
                switch (c)
                {
                    case '\n':
                        PreAnalyse();
                        stateCode = StateCode.Init;
                        break;
                    case '\\':
                        beforeEscape = stateCode;
                        stateCode = StateCode.Comment10;
                        break;
                }
                break;
            case StateCode.Comment2:
                switch (c)
                {
                    case '*':
                        stateCode = StateCode.Comment3;
                        break;
                    case '\n':
                        PreAnalyse();
                        break;
                }
                break;
            case StateCode.Comment3:
                switch (c)
                {
                    case '/':
                        stateCode = beforeComment;
                        break;
                    case '*':
                        break;
                    case '\n':
                        PreAnalyse();
                        stateCode = StateCode.Comment2;
                        break;
                    default:
                        stateCode = StateCode.Comment2;
                        break;
                }
                break;
            case StateCode.Comment10:
                switch (c)
                {
                    case '\r':
                        stateCode = StateCode.Comment11;
                        break;
                    case '\n':
                    default:
                        stateCode = beforeEscape;
                        break;
                }
                break;
            case StateCode.Comment11:
                switch (c)
                {
                    case '\\':
                        stateCode = StateCode.Comment10;
                        break;
                    case '\n':
                    default:
                        stateCode = beforeEscape;
                        break;
                }
                break;
            case StateCode.Escape0:
                switch (c)
                {
                    case 'n':
                        buffer.Append('\n');
                        break;
                    case 'r':
                        buffer.Append('\r');
                        break;
                    case 't':
                        buffer.Append('\t');
                        break;
                    case 'a':
                        buffer.Append('\a');
                        break;
                    case '\r':
                    case '\n':
                        break;
                    case '\\':
                        buffer.Append(c);
                        break;
                }
                if (c == '\r')
                    stateCode = StateCode.Escape1;
                else
                    stateCode = beforeEscape;
                break;
            case StateCode.Escape1:
                switch (c)
                {
                    case '\\':
                        stateCode = StateCode.Escape0;
                        break;
                    case '\n':
                        stateCode = beforeEscape;
                        break;
                    default:
                        stateCode = beforeEscape;
                        SourceInput(c);
                        break;
                }
                break;
        }
    }

    void AddToken()
    {
        if (buffer.Length > 0)
        {
            analysis.Add(buffer.ToString());
            buffer.Clear();
        }
    }

    void PreAnalyse()
    {
        int state = 0;
        foreach (var token in analysis)
        {
            if (state == 0)
            {
                if (token == "pragma")
                {
                    state = 1;
                }
                else
                    break;
            }
            else if (state == 1)
            {
                if (token == "VertexShader")
                {
                    state = 2;
                }
                else if (token == "PixelShader")
                {
                    state = 3;
                }
                else if (token == "GeometryShader")
                {
                    state = 4;
                }
                else if (token == "Blend")
                {
                    state = 5;
                }
                else if (token == "ExportDefine")
                {
                    state = 101;
                }
                else
                    break;
            }
            else if (state == 2)
            {
                vertexShader = token;
                break;
            }
            else if (state == 3)
            {
                pixelShader = token;
                break;
            }
            else if (state == 4)
            {
                geometryShader = token;
                break;
            }
            else if (state == 5)
            {
                blend = token;
                break;
            }
            else if (state == 101)
            {
                exportDefines.Add(token);

                state = 102;
            }
            else if (state == 102)
            {
                break;
            }
        }
        analysis.Clear();
    }
    StateCode stateCode;
    StateCode beforeComment;
    StateCode beforeEscape;

    public string vertexShader;
    public string geometryShader;
    public string pixelShader;
    public string blend;

    public List<string> exportDefines = new List<string>();


    StringBuilder buffer = new();
    List<string> analysis = new();

    enum StateCode
    {
        Init = 0,
        Pre0,
        //Pre1,
        //Pre11,
        Comment0,
        Comment1,
        Comment10,
        Comment11,
        Comment2,
        Comment3,
        Escape0,
        Escape1,
    }
}
