const caseStageLabels = {
  not_read: 'Not read yet',
  needs_attention: 'Needs attention',
  ready_for_accounting: 'Ready for accounting',
}

export type CaseStage = keyof typeof caseStageLabels

export function caseStageLabel(stage: CaseStage): string {
  return caseStageLabels[stage]
}
