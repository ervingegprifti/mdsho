using System;

namespace MDSHO.Common {

  /// <summary>Creates the identifiers used to name the shortcut files in the shortcuts folder.</summary>
  public static class Identifiers {
    /// <summary>A new random GUID as upper-case text with dashes; also the name (without extension) of a shortcut's file.</summary>
    public static string NewGuid() {
      return Guid.NewGuid().ToString().ToUpper();
    }
  }

}
