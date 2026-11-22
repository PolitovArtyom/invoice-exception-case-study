const caseStageLabels = {
  not_read: 'Not read yet',
  needs_attention: 'Needs attention',
  ready_for_accounting: 'Ready for accounting',
  closed: 'Closed',
}

export type CaseStage = keyof typeof caseStageLabels
export type CaseTab = Exclude<CaseStage, 'not_read'>

export const simulatedHandover = 'Handed to accounting (simulated)'

export function caseStageLabel(stage: CaseStage): string {
  return caseStageLabels[stage]
}
