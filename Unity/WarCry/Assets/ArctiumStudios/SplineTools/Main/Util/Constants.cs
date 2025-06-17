namespace ArctiumStudios.SplineTools
{
    public static class Constants
    {
        /// <summary>
        /// Key for List&lt;Vector2&gt; outline points of a lake. Points are always ordered counter-clockwise.
        /// </summary>
        public const string LakeOutline = "__LakeOutline";
        
        /// <summary>
        /// Key for Vector3 direction of a river. Stored with nodes of type <see cref="NodeBaseType.SectionRiverFordSource"/>
        /// &amp; <see cref="NodeBaseType.SectionRiverFordDestination"/>.
        /// </summary>
        public const string RiverDirection = "__RiverDirection";
    }
}