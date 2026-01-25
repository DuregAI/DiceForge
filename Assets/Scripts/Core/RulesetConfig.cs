using System;

namespace Diceforge.Core
{
    [Serializable]
    public sealed class RulesetConfig
    {
        // Модель "камни игроков" на кольце.
        // - ringSize: размер кольца (0..ringSize-1)
        // - оба игрока двигаются "вперёд" по кольцу (по часовой)
        // - камни двигаются по броску кубика

        public int ringSize = 9;

        // максимальный бросок кубика
        public int maxRoll = 6;

        // всего камней у игрока на матч
        public int totalStonesPerPlayer = 15;

        // сколько камней сразу стоит на старте
        public int startStonesPerPlayer = 5;

        // стартовые клетки для игроков
        public int startCellA = 0;
        public int startCellB = 4;

        // разрешать "hit" одиночного камня соперника
        public bool allowHitSingleStone = true;

        // лимит ходов на всякий случай
        public int maxTurns = 60;

        // сид для повторяемости боя
        public int randomSeed = 12345;

        // логировать каждый ход
        public bool verboseLog = true;

        public void Validate()
        {
            ringSize = Math.Clamp(ringSize, 3, 99);
            maxRoll = Math.Clamp(maxRoll, 0, 20);
            totalStonesPerPlayer = Math.Clamp(totalStonesPerPlayer, 0, 99);
            startStonesPerPlayer = Math.Clamp(startStonesPerPlayer, 0, totalStonesPerPlayer);
            startCellA = Math.Clamp(startCellA, 0, ringSize - 1);
            startCellB = Math.Clamp(startCellB, 0, ringSize - 1);
            maxTurns = Math.Clamp(maxTurns, 1, 9999);
        }
    }
}
