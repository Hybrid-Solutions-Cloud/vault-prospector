using Android.App.Assist;
using VaultProspector.Mobile.Core;

namespace VaultProspector.Mobile.Android;

internal static class AndroidAutofillRequestParser
{
    private const int MaximumNodes = 4096;
    private const int MaximumDepth = 32;

    public static bool TryAnalyze(
        AssistStructure structure,
        out MobileAutofillCandidate? candidate)
    {
        ArgumentNullException.ThrowIfNull(structure);
        candidate = null;

        var origins = new HashSet<(string Scheme, string Domain)>();
        var fields = new List<MobileAutofillFieldDescriptor>();
        var nodesVisited = 0;

        for (var index = 0; index < structure.WindowNodeCount; index++)
        {
            var root = structure.GetWindowNodeAt(index)?.RootViewNode;
            if (root is null ||
                !Visit(root, 0, origins, fields, ref nodesVisited))
            {
                return false;
            }
        }

        if (origins.Count != 1)
            return false;

        var origin = origins.Single();
        return MobileAutofillRequestAnalyzer.TryAnalyze(
            new MobileAutofillNativeRequest(
                origin.Scheme,
                origin.Domain,
                fields),
            out candidate);
    }

    private static bool Visit(
        AssistStructure.ViewNode node,
        int depth,
        HashSet<(string Scheme, string Domain)> origins,
        List<MobileAutofillFieldDescriptor> fields,
        ref int nodesVisited)
    {
        if (depth > MaximumDepth || ++nodesVisited > MaximumNodes)
            return false;

        var scheme = node.WebScheme;
        var domain = node.WebDomain;
        if (!string.IsNullOrWhiteSpace(scheme) ||
            !string.IsNullOrWhiteSpace(domain))
        {
            if (string.IsNullOrWhiteSpace(scheme) ||
                string.IsNullOrWhiteSpace(domain))
            {
                return false;
            }

            origins.Add((scheme, domain));
            if (origins.Count > 1)
                return false;
        }

        var autofillId = node.AutofillId;
        var hints = node.GetAutofillHints();
        if (autofillId is not null && hints is { Length: > 0 })
        {
            fields.Add(new MobileAutofillFieldDescriptor(
                autofillId.ToString(),
                hints));
        }

        for (var index = 0; index < node.ChildCount; index++)
        {
            var child = node.GetChildAt(index);
            if (child is null ||
                !Visit(
                    child,
                    depth + 1,
                    origins,
                    fields,
                    ref nodesVisited))
            {
                return false;
            }
        }

        return true;
    }
}
