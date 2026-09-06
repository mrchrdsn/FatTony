using System.Text;
using FatTony.Ast;

namespace FatTony.Parsing;

public static class AstPrinter
{
    public static string Print(ProgramNode program)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Program (business: {program.NamespaceName})");
        foreach (var item in program.Items)
            PrintNode(sb, item, 1);
        return sb.ToString();
    }

    private static void Indent(StringBuilder sb, int depth) => sb.Append(new string(' ', depth * 2));

    private static void PrintNode(StringBuilder sb, AstNode node, int depth)
    {
        Indent(sb, depth);
        switch (node)
        {
            case ConnectedDecl c:
                sb.AppendLine($"ConnectedDecl -> {c.NamespaceName}");
                break;

            case FunctionDecl f:
                sb.AppendLine($"FunctionDecl {f.Name}({string.Join(", ", f.Params)})");
                foreach (var s in f.Body) PrintNode(sb, s, depth + 1);
                break;

            case ThingDecl t:
                sb.AppendLine($"ThingDecl {t.Name}" + (t.ParentName is null ? "" : $" answers to {t.ParentName}"));
                foreach (var field in t.Fields) PrintNode(sb, field, depth + 1);
                if (t.Initiation is not null) PrintNode(sb, t.Initiation, depth + 1);
                foreach (var m in t.Methods) PrintNode(sb, m, depth + 1);
                break;

            case FieldDecl fd:
                sb.AppendLine($"FieldDecl {fd.Name} = {Expr(fd.Default)}");
                break;

            case InitiationDecl init:
                sb.AppendLine($"InitiationDecl({string.Join(", ", init.Params)})");
                foreach (var s in init.Body) PrintNode(sb, s, depth + 1);
                break;

            case LetStmt let:
                sb.AppendLine($"Let {Expr(let.Target)} = {Expr(let.Value)}");
                break;

            case SayStmt say:
                sb.AppendLine($"Say {Expr(say.Value)}");
                break;

            case IfStmt ifs:
                sb.AppendLine("If");
                for (int i = 0; i < ifs.Branches.Count; i++)
                {
                    Indent(sb, depth + 1);
                    sb.AppendLine($"{(i == 0 ? "if" : "otherwise if")} {Expr(ifs.Branches[i].Condition)}");
                    foreach (var s in ifs.Branches[i].Body) PrintNode(sb, s, depth + 2);
                }
                if (ifs.ElseBody is not null)
                {
                    Indent(sb, depth + 1);
                    sb.AppendLine("otherwise");
                    foreach (var s in ifs.ElseBody) PrintNode(sb, s, depth + 2);
                }
                break;

            case RepeatWhileStmt rw:
                sb.AppendLine($"RepeatWhile {Expr(rw.Condition)}");
                foreach (var s in rw.Body) PrintNode(sb, s, depth + 1);
                break;

            case CallStmt cs:
                sb.AppendLine($"CallStmt {Expr(cs.Call)}");
                break;

            case ReturnStmt ret:
                sb.AppendLine(ret.Value is null ? "Return (bare)" : $"Return {Expr(ret.Value)}");
                break;

            case IncreaseStmt inc:
                sb.AppendLine($"Increase {Expr(inc.Target)}" + (inc.Amount is null ? "" : $" by {Expr(inc.Amount)}"));
                break;

            case DecreaseStmt dec:
                sb.AppendLine($"Decrease {Expr(dec.Target)}" + (dec.Amount is null ? "" : $" by {Expr(dec.Amount)}"));
                break;

            case BreakStmt: sb.AppendLine("Break (walk away)"); break;
            case ContinueStmt: sb.AppendLine("Continue (keep it movin')"); break;
            case TrashStmt: sb.AppendLine("TakeOutDaTrash"); break;

            case GetLedgerStmt g:
                sb.AppendLine($"GetLedger {g.LedgerName} [{g.Mode}]");
                break;

            case WriteStmt w:
                sb.AppendLine($"Write {Expr(w.Data)} -> {w.LedgerName}");
                break;

            case StashStmt st:
                sb.AppendLine($"Stash {st.LedgerName}");
                break;

            case WhackVarStmt wv:
                sb.AppendLine($"WhackVar {Expr(wv.Target)}");
                break;

            case WhackLedgerEntryStmt wl:
                sb.AppendLine($"WhackLedgerEntry {Expr(wl.LineRef)} from {wl.LedgerName}");
                break;

            case BurnStmt b:
                sb.AppendLine($"Burn {b.LedgerName}");
                break;

            case DestroyStmt d:
                sb.AppendLine($"Destroy {d.InstanceName}");
                break;

            default:
                sb.AppendLine($"<unhandled node {node.GetType().Name}>");
                break;
        }
    }

    private static string Expr(Ast.Expr e) => e switch
    {
        NumberLiteral n => n.Value.ToString(),
        StringLiteral s => $"\"{s.Value}\"",
        BoolLiteral b => b.Value ? "LEGIT" : "SHADY",
        Assignable a => (a.IsYoursTruly ? "yours truly" : a.Name) + (a.Field is null ? "" : $"'s {a.Field}"),
        BinaryExpr bin => $"({Expr(bin.Left)} {bin.Op} {Expr(bin.Right)})",
        UnaryMinusExpr u => $"(-{Expr(u.Operand)})",
        FunctionCallExpr fc => $"doMeAFavor {fc.FunctionName}({string.Join(", ", fc.Args.Select(Expr))})",
        MethodCallExpr mc => $"use {(mc.IsYoursTruly ? "yours truly" : mc.Target)} toDoMeAFavor {mc.FunctionName}({string.Join(", ", mc.Args.Select(Expr))})",
        LedgerReadExpr lr => $"daStoryFrom {lr.LedgerName}",
        InputReadExpr => "whatDaGuySaid",
        InstantiationExpr inst => $"aNew {inst.ThingName}({string.Join(", ", inst.Args.Select(Expr))})",
        _ => $"<unhandled expr {e.GetType().Name}>"
    };
}
