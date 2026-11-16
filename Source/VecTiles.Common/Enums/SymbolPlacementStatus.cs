namespace VecTiles.Common.Enums;

/// <summary>
/// Result of attempting to place a symbol (icon or text) on the canvas.
/// </summary>
public enum SymbolPlacementStatus
{
    /// <summary>Symbol can be placed; space is available.</summary>
    Success,
    
    /// <summary>Symbol cannot be placed; its envelope is not initialized.</summary>
    NotInitialized,
    
    /// <summary>Symbol cannot be placed; it is outside the visible viewport.</summary>
    OutOfBounds,
    
    /// <summary>Symbol cannot be placed; the space is occupied by another symbol.</summary>
    Collision
}
