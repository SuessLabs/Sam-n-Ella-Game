// Copyright Xeno Innovations, Inc. 2026
// See the LICENSE file in the project root for more information.

using System.Globalization;
using SamElla.Actors;

namespace SamElla;

internal sealed class EnemyConfiguration
{
  public static EnemySettings DefaultCreepyCrawler { get; } = new(1.5, 2, 0);
  public static EnemySettings DefaultDog { get; } = new(2, 1, 0);
  public static PlayerSettings DefaultPlayerPowerLevel1 { get; } = new(12, 1, 15);
  public static PlayerSettings DefaultPlayerPowerLevel2 { get; } = new(12, 2, 15);
  public static EnemySettings DefaultToasterCat { get; } = new(6, 2, 7);
  public static EnemySettings DefaultWalker { get; } = new(3, 1, 0);
  public static EnemySettings DefaultZombie { get; } = new(1, 3, 0);
  public EnemySettings CreepyCrawler { get; private set; } = DefaultCreepyCrawler;
  public EnemySettings Dog { get; private set; } = DefaultDog;
  public PlayerSettings PlayerPowerLevel1 { get; private set; } = DefaultPlayerPowerLevel1;
  public PlayerSettings PlayerPowerLevel2 { get; private set; } = DefaultPlayerPowerLevel2;
  public EnemySettings ToasterCat { get; private set; } = DefaultToasterCat;
  public EnemySettings Walker { get; private set; } = DefaultWalker;
  public EnemySettings Zombie { get; private set; } = DefaultZombie;

  public static EnemyConfiguration Load(string path)
  {
    var config = new EnemyConfiguration();
    if (!File.Exists(path))
      return config;

    string section = string.Empty;
    int lineNumber = 0;

    foreach (string raw in File.ReadLines(path))
    {
      lineNumber++;
      string line = raw.Trim();
      if (line.Length == 0 || line.StartsWith(';'))
        continue;

      if (line is "[Walker]" or "[ToasterCat]" or "[Zombie]" or "[Dog]" or "[Creepy-Crawler]" or "[Player.PowerLevel1]" or "[Player.PowerLevel2]")
      {
        section = line[1..^1];
        continue;
      }

      string[] parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
      if (section.Length == 0 || parts.Length != 2)
        throw new InvalidDataException($"{path}:{lineNumber}: expected an enemy/player section or Setting=Value.");

      (double Speed, int PowerLevel, double HopVelocity) current = section switch
      {
        "Walker" => (config.Walker.Speed, config.Walker.PowerLevel, config.Walker.HopVelocity),
        "ToasterCat" => (config.ToasterCat.Speed, config.ToasterCat.PowerLevel, config.ToasterCat.HopVelocity),
        "Zombie" => (config.Zombie.Speed, config.Zombie.PowerLevel, config.Zombie.HopVelocity),
        "Dog" => (config.Dog.Speed, config.Dog.PowerLevel, config.Dog.HopVelocity),
        "Creepy-Crawler" => (config.CreepyCrawler.Speed, config.CreepyCrawler.PowerLevel, config.CreepyCrawler.HopVelocity),
        "Player.PowerLevel1" => (config.PlayerPowerLevel1.Speed, config.PlayerPowerLevel1.PowerLevel, config.PlayerPowerLevel1.HopVelocity),
        _ => (config.PlayerPowerLevel2.Speed, config.PlayerPowerLevel2.PowerLevel, config.PlayerPowerLevel2.HopVelocity),
      };
      try
      {
        double speed = current.Speed;
        int power = current.PowerLevel;
        double hop = current.HopVelocity;
        switch (parts[0])
        {
          case "Speed":
            speed = double.Parse(parts[1], CultureInfo.InvariantCulture);
            break;

          case "PowerLevel":
            power = int.Parse(parts[1], CultureInfo.InvariantCulture);
            break;

          case "HopVelocity":
            hop = double.Parse(parts[1], CultureInfo.InvariantCulture);
            break;

          default:
            throw new InvalidDataException($"{path}:{lineNumber}: unknown setting '{parts[0]}'.");
        }

        switch (section)
        {
          case "Walker":
            config.Walker = new EnemySettings(speed, power, hop);
            break;

          case "ToasterCat":
            config.ToasterCat = new EnemySettings(speed, power, hop);
            break;

          case "Zombie":
            config.Zombie = new EnemySettings(speed, power, hop);
            break;

          case "Dog":
            config.Dog = new EnemySettings(speed, power, hop);
            break;

          case "Creepy-Crawler":
            config.CreepyCrawler = new EnemySettings(speed, power, hop);
            break;

          case "Player.PowerLevel1":
            config.PlayerPowerLevel1 = new PlayerSettings(speed, power, hop);
            break;

          case "Player.PowerLevel2":
            config.PlayerPowerLevel2 = new PlayerSettings(speed, power, hop);
            break;
        }
      }
      catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentOutOfRangeException)
      {
        throw new InvalidDataException($"{path}:{lineNumber}: invalid {parts[0]} value '{parts[1]}'. {exception.Message}", exception);
      }
    }

    return config;
  }
}
