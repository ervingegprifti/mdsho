using MDSHO.Core;

namespace MDSHO.Data {

  /// <summary>The DefaultColors table: one row per <see cref="ColorSetKind"/>, keyed by its name (see <see cref="IDefaultColorsRepository"/>).</summary>
  internal sealed class DefaultColorsRepository : RepositoryBase, IDefaultColorsRepository {

    public ColorSet Get(ColorSetKind kind) {
      return One<ColorSet>(
        "SELECT BorderColor, TitleBackgroundColor, TitleTextColor, BackgroundColor, TextColor FROM DefaultColors WHERE Kind = @kind",
        new { kind = kind.ToString() });
    }

    public void Save(ColorSetKind kind, ColorSet colors) {
      Run(@"
        INSERT INTO DefaultColors (Kind, BorderColor, TitleBackgroundColor, TitleTextColor, BackgroundColor, TextColor)
        VALUES (@Kind, @BorderColor, @TitleBackgroundColor, @TitleTextColor, @BackgroundColor, @TextColor)
        ON CONFLICT(Kind) DO UPDATE SET
          BorderColor = excluded.BorderColor, TitleBackgroundColor = excluded.TitleBackgroundColor,
          TitleTextColor = excluded.TitleTextColor, BackgroundColor = excluded.BackgroundColor, TextColor = excluded.TextColor",
        new {
          Kind = kind.ToString(), colors.BorderColor, colors.TitleBackgroundColor, colors.TitleTextColor, colors.BackgroundColor, colors.TextColor
        });
    }
  }
}
