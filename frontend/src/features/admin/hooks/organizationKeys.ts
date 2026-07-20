export const organizations = {
  all: ['organizations'] as const,
  list: () => [...organizations.all, 'list'] as const,
}

export const organizationKeys = { organizations }
