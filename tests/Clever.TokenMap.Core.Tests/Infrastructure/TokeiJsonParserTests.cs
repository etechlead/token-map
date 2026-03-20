using Clever.TokenMap.Infrastructure.Tokei;

namespace Clever.TokenMap.Core.Tests.Infrastructure;

public sealed class TokeiJsonParserTests
{
    [Fact]
    public void Parse_MapsReportsToNormalizedRelativePaths()
    {
        const string json = """
                            {
                              "Plain Text": {
                                "blanks": 1,
                                "code": 2,
                                "comments": 0,
                                "inaccurate": false,
                                "reports": [
                                  {
                                    "name": ".\\nested\\keep.txt",
                                    "stats": {
                                      "blanks": 1,
                                      "blobs": {},
                                      "code": 2,
                                      "comments": 0
                                    }
                                  }
                                ]
                              },
                              "PowerShell": {
                                "blanks": 0,
                                "code": 1,
                                "comments": 2,
                                "inaccurate": false,
                                "reports": [
                                  {
                                    "name": ".\\scripts\\build.ps1",
                                    "stats": {
                                      "blanks": 0,
                                      "blobs": {},
                                      "code": 1,
                                      "comments": 2
                                    }
                                  }
                                ]
                              },
                              "Total": {
                                "blanks": 1,
                                "code": 3,
                                "comments": 2,
                                "inaccurate": false,
                                "reports": []
                              }
                            }
                            """;

        var parser = new TokeiJsonParser();
        var result = parser.Parse(json, ["nested/keep.txt"]);

        var stats = Assert.Single(result);
        Assert.Equal("nested/keep.txt", stats.Key);
        Assert.Equal(3, stats.Value.TotalLines);
        Assert.Equal(2, stats.Value.CodeLines);
        Assert.Equal(0, stats.Value.CommentLines);
        Assert.Equal(1, stats.Value.BlankLines);
        Assert.Equal("Plain Text", stats.Value.Language);
    }
}
