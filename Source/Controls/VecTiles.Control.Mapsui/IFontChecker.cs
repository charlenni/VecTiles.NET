namespace VecTiles.Controls.Mapsui;

/// <summary>
/// Implemented by rendered tile sources that know the font names of their
/// style file, so that <see cref="RenderedTileLayer"/> can check them for
/// missing fonts AFTER the font resolvers passed to the layer constructor
/// were registered: a font that the font source resolver can provide would
/// wrongly be reported as missing before that.
/// </summary>
internal interface IFontChecker
{
    /// <summary>
    /// Reports the font names of the style file that cannot be resolved.
    /// </summary>
    void ReportMissingFonts();
}
