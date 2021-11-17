using System;
using System.Linq;
using System.Threading;
using Nhl.Api;
using Nhl.Api.Models.Game;
using Nhl.Api.Models.Team;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

const int width = 64;
const int height = 64;
const int scalingFactor = 10;
const int logoWidth = (int)(.333 * width * scalingFactor);
const int logoHeight = (int)(.333 * height * scalingFactor);
const int topGutterHeight = (int)(height * .03 * scalingFactor);
const int leftGutterWidth = (int)(.333 / 2 / 2 * width * scalingFactor);
const int middleGutterWidth = (int)(.333 / 2 * width * scalingFactor);
var client = new NhlApi();
var date = DateTime.UtcNow;
var games = client.GetGameScheduleByDateAsync(date).Result;
while (true)
{
    if (date < DateTime.UtcNow)
    {
        date = DateTime.UtcNow;
    }

    if (games.Dates.All(d => d.Date.Date != date.Date))
    {
        games = client.GetGameScheduleByDateAsync(date).Result;
    }

    var found = false;
    games.Dates.ForEach(d =>
    {
        Console.WriteLine(d.Date.ToString("dddd, M/d/yy"));
        var predsGame = d.Games.FirstOrDefault(g =>
            !g.Status.DetailedState.Contains("Postponed") &&
            g.Teams.AwayTeam.Team.Name.Contains("Nashville") ||
            g.Teams.HomeTeam.Team.Name.Contains("Nashville")
        );
        if (predsGame == null)
        {
            return;
        }

        found = true;
        var game = client.GetLiveGameFeedById(predsGame.GamePk).Result;
        using var image = new Image<Rgba32>(width * scalingFactor, height * scalingFactor);
        var collection = new FontCollection();
        var family = collection.Install("./fonts/Blockletter.otf");
        image.Mutate(x => x.Fill(Color.Black));
        DrawLogos(image);
        DrawPeriodAndTime(image, family, game);
        DrawScore(image, family, game);
        DrawSOG(image, family, game);
        DrawGameTimeInformation(image, family, game);
        DrawPowerPlay(image, family, game);
        image.Save("output.bmp");
        Console.WriteLine(
            $"{predsGame.Teams.AwayTeam.Team.Name} {GetTeamRecord(predsGame.Teams.AwayTeam)} vs {predsGame.Teams.HomeTeam.Team.Name} {GetTeamRecord(predsGame.Teams.HomeTeam)}");
        Console.WriteLine($"{predsGame.GameDate.ToLocalTime():h:mm tt}");
        Console.WriteLine($"Period: {game.LiveGameFeed.LiveData.Linescore.CurrentPeriod}");
        Console.WriteLine($"Time Remaining: {game.LiveGameFeed.LiveData.Linescore.CurrentPeriodTimeRemaining}");
        Console.WriteLine(
            $"Score: {game.LiveGameFeed.LiveData.Linescore.Teams.Away.Goals} - {game.LiveGameFeed.LiveData.Linescore.Teams.Home.Goals}");
    });
    if (!found)
    {
        date = date.AddDays(1);
    }

    Thread.Sleep(500);
}

static string GetTeamRecord(TeamWithLeagueRecord team)
{
    return $"({team.LeagueRecord.Wins}-{team.LeagueRecord.Losses}-{team.LeagueRecord.Ot})";
}

void DrawLogos(Image<Rgba32>? image)
{
    using var predsLogo = Image.Load("./logos/predsLogo.png");
    using var cbjLogo = Image.Load("./logos/redwingsLogo.png");

    predsLogo.Mutate(x => x.Resize(logoWidth, logoHeight));
    cbjLogo.Mutate(x => x.Resize(logoWidth, logoHeight));
    image.Mutate(x => x.DrawImage(predsLogo, new Point(leftGutterWidth, topGutterHeight), 1));
    image.Mutate(x =>
        x.DrawImage(cbjLogo, new Point(leftGutterWidth + logoWidth + middleGutterWidth, topGutterHeight), 1));
}

void DrawPeriodAndTime(Image<Rgba32>? image, FontFamily fontFamily, LiveGameFeedResult? game)
{
    var font = fontFamily.CreateFont((float)(height * scalingFactor * .12), FontStyle.Regular);
    var textOptions = new TextOptions
    {
        WrapTextWidth = width * scalingFactor,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };
    var drawingOptions = new DrawingOptions
    {
        TextOptions = textOptions
    };
    var period = $"P{game.LiveGameFeed.LiveData.Linescore.CurrentPeriod}";
    image.Mutate(x => x.DrawText(drawingOptions, period, font, Color.White,
        new PointF(0, (float)(height * scalingFactor * .5))));
    var time = $"{game.LiveGameFeed.LiveData.Linescore.CurrentPeriodTimeRemaining}";
    image.Mutate(x => x.DrawText(drawingOptions, time, font, Color.White,
        new PointF(0, (float)(height * scalingFactor * .5 + font.Size))));
}

void DrawScore(Image<Rgba32>? image, FontFamily fontFamily, LiveGameFeedResult? game)
{
    var font = fontFamily.CreateFont((float)(height * scalingFactor * .4), FontStyle.Regular);
    var textOptions = new TextOptions
    {
        WrapTextWidth = width / 2 * scalingFactor,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Bottom
    };
    var drawingOptions = new DrawingOptions
    {
        TextOptions = textOptions
    };
    var awayScore = $"{game.LiveGameFeed.LiveData.Linescore.Teams.Away.Goals}";
    // if (game.LiveGameFeed.LiveData.Linescore.Teams.Away.PowerPlay)
    // {
    var pen = new Pen(Color.Red, 3);
    image.Mutate(x => x.DrawText(drawingOptions, awayScore, font, Brushes.Solid(Color.White), pen,
        new PointF(0, (float)((topGutterHeight + logoHeight + font.Size) * 1.05))));
    // }
    // else
    // {
    //     image.Mutate(x => x.DrawText(drawingOptions, awayScore, font, Color.White,
    //         new PointF(0, (float)((topGutterHeight + logoHeight + font.Size) * 1.05))));
    // }
    var homeScore = $"{game.LiveGameFeed.LiveData.Linescore.Teams.Home.Goals}";
    if (game.LiveGameFeed.LiveData.Linescore.Teams.Home.PowerPlay)
    {
        // var pen = new Pen(Color.Red, 2);
        image.Mutate(x => x.DrawText(drawingOptions, homeScore, font, Brushes.Solid(Color.White), pen,
            new PointF(width / 2 * scalingFactor, (float)((topGutterHeight + logoHeight + font.Size) * 1.05))));
    }
    else
    {
        image.Mutate(x => x.DrawText(drawingOptions, homeScore, font, Color.White,
            new PointF(width / 2 * scalingFactor, (float)((topGutterHeight + logoHeight + font.Size) * 1.05))));
    }
}

void DrawSOG(Image<Rgba32>? image, FontFamily fontFamily, LiveGameFeedResult? game)
{
    var font = fontFamily.CreateFont((float)(height * scalingFactor * .1), FontStyle.Regular);
    var textOptions = new TextOptions
    {
        WrapTextWidth = width / 2 * scalingFactor,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Bottom
    };
    var drawingOptions = new DrawingOptions
    {
        TextOptions = textOptions
    };
    var SogText = "SOG";
    image.Mutate(x => x.DrawText(drawingOptions, SogText, font, Color.White,
        new PointF(0, (float)(height * scalingFactor * .77))));
    image.Mutate(x => x.DrawText(drawingOptions, SogText, font, Color.White,
        new PointF(width / 2 * scalingFactor, (float)(height * scalingFactor * .77))));

    var awayShots = $"{game.LiveGameFeed.LiveData.Linescore.Teams.Away.ShotsOnGoal}";
    image.Mutate(x => x.DrawText(drawingOptions, awayShots, font, Color.White,
        new PointF(0, (float)(height * scalingFactor * .77 + font.Size))));
    var homeShots = $"{game.LiveGameFeed.LiveData.Linescore.Teams.Home.ShotsOnGoal}";
    image.Mutate(x => x.DrawText(drawingOptions, homeShots, font, Color.White,
        new PointF(width / 2 * scalingFactor, (float)(height * scalingFactor * .77 + font.Size))));
}

void DrawGameTimeInformation(Image<Rgba32>? image, FontFamily fontFamily, LiveGameFeedResult? game)
{
    var font = fontFamily.CreateFont((float)(height * scalingFactor * .08), FontStyle.Regular);
    var textOptions = new TextOptions
    {
        WrapTextWidth = width * scalingFactor,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Bottom
    };
    var drawingOptions = new DrawingOptions
    {
        TextOptions = textOptions
    };
    var localDate = game.LiveGameFeed.GameData.Datetime.DateTime.ToLocalTime();
    var dayOfWeek = localDate.ToString("ddd");
    var monthAndDay = localDate.ToString("M/d");
    var time = localDate.ToString("h:mm tt");
    image.Mutate(x => x.DrawText(drawingOptions, dayOfWeek, font, Color.White,
        new PointF(0, (float)(height * scalingFactor * .85))));
    image.Mutate(x => x.DrawText(drawingOptions, monthAndDay, font, Color.White,
        new PointF(0, (float)(height * scalingFactor * .85 + font.Size))));
    image.Mutate(x => x.DrawText(drawingOptions, time, font, Color.White,
        new PointF(0, (float)(height * scalingFactor * .85 + font.Size + font.Size))));
}

void DrawPowerPlay(Image<Rgba32>? image, FontFamily fontFamily, LiveGameFeedResult? game)
{
    var font = fontFamily.CreateFont((float)(height * scalingFactor * .08), FontStyle.Regular);
    var textOptions = new TextOptions
    {
        WrapTextWidth = width / 2 * scalingFactor,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Bottom
    };
    var drawingOptions = new DrawingOptions
    {
        TextOptions = textOptions
    };
    // if (game.LiveGameFeed.LiveData.Linescore.PowerPlayInfo.InSituation)
    // {
    // var timeLeft = TimeSpan.FromSeconds(game.LiveGameFeed.LiveData.Linescore.PowerPlayInfo.SituationTimeRemaining);
    var timeLeft = TimeSpan.FromSeconds(120);
    // var strength = game.LiveGameFeed.LiveData.Linescore.PowerPlayStrength;
    var strength = "5 on 3";
    // if (game.LiveGameFeed.LiveData.Linescore.Teams.Away.PowerPlay)
    // {
    image.Mutate(x => x.DrawText(drawingOptions, strength, font, Color.White,
        new PointF(0, (float)(height * scalingFactor * .93))));
    image.Mutate(x => x.DrawText(drawingOptions, timeLeft.ToString(@"m\:ss"), font, Color.Red,
        new PointF(0, (float)(height * scalingFactor * .93 + font.Size))));
    // }
    // else
    // {
    // image.Mutate(x => x.DrawText(drawingOptions, strength, font, Color.White,
    //     new PointF(width / 2 * scalingFactor, (float)(height * scalingFactor * .93))));
    // image.Mutate(x => x.DrawText(drawingOptions, timeLeft.ToString(@"m\:ss"), font, Color.Red,
    //     new PointF(width / 2 * scalingFactor, (float)(height * scalingFactor * .93 + font.Size))));
    // }
}