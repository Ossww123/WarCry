#if ST_MM_2

using MapMagic.Nodes;

namespace ArctiumStudios.SplineTools
{
    [GeneratorMenu(menu = "SplineTools/Portals", name = "WorldGraph Enter", iconName = "GeneratorIcons/PortalIn", lookLikePortal=true,
        colorType = typeof(WorldGraphGuid))]
    public class WorldGraphPortalEnter : PortalEnter<WorldGraphGuid>, SplineToolsGenerator
    {
    }

    [GeneratorMenu(menu = "SplineTools/Portals", name = "WorldGraph Exit", iconName = "GeneratorIcons/PortalOut", lookLikePortal=true,
        colorType = typeof(WorldGraphGuid))]
    public class WorldGraphPortalExit : PortalExit<WorldGraphGuid>, SplineToolsGenerator
    {
    }

    [GeneratorMenu(menu = "SplineTools/Portals", name = "Bounds Enter", iconName = "GeneratorIcons/PortalIn", lookLikePortal=true,
        colorType = typeof(Bounds))]
    public class BoundsPortalEnter : PortalEnter<Bounds>, SplineToolsGenerator
    {
    }

    [GeneratorMenu(menu = "SplineTools/Portals", name = "Bounds Exit", iconName = "GeneratorIcons/PortalOut", lookLikePortal=true,
        colorType = typeof(Bounds))]
    public class BoundsPortalExit : PortalExit<Bounds>, SplineToolsGenerator
    {
    }

    [GeneratorMenu(menu = "SplineTools/Portals", name = "Edges Enter", iconName = "GeneratorIcons/PortalIn", lookLikePortal=true,
        colorType = typeof(EdgesByOffset))]
    public class EdgesPortalEnter : PortalEnter<EdgesByOffset>, SplineToolsGenerator
    {
    }

    [GeneratorMenu(menu = "SplineTools/Portals", name = "Edges Exit", iconName = "GeneratorIcons/PortalOut", lookLikePortal=true,
        colorType = typeof(EdgesByOffset))]
    public class EdgesPortalExit : PortalExit<EdgesByOffset>, SplineToolsGenerator
    {
    }
    
    [GeneratorMenu(menu = "SplineTools/Portals", name = "Nodes Enter", iconName = "GeneratorIcons/PortalIn", lookLikePortal=true,
        colorType = typeof(NodesByOffset))]
    public class NodesPortalEnter : PortalEnter<NodesByOffset>, SplineToolsGenerator
    {
    }

    [GeneratorMenu(menu = "SplineTools/Portals", name = "Nodes Exit", iconName = "GeneratorIcons/PortalOut", lookLikePortal=true,
        colorType = typeof(NodesByOffset))]
    public class NodesPortalExit : PortalExit<NodesByOffset>, SplineToolsGenerator
    {
    }
}

#endif
