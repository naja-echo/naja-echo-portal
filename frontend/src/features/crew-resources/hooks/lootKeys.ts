export const lootKeys = {
  all: ['loot'] as const,
  myLoot: () => [...lootKeys.all, 'me'] as const,
  distribution: () => [...lootKeys.all, 'distribution'] as const,
  memberLedger: (userId: string) => [...lootKeys.all, 'member', userId] as const,
}
