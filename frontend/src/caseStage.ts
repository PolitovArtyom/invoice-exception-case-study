const caseStageLabels = {
  not_read: 'Not read yet',
  needs_attention: 'Needs attention',
  ready_for_accounting: 'Ready for accounting',
  accepted_with_discrepancy: 'Ready for accounting · Accepted with discrepancy',
  closed: 'Closed',
}

export type CaseStage = keyof typeof caseStageLabels
export type CaseTab = Exclude<
  CaseStage,
  'not_read' | 'accepted_with_discrepancy'
>

export const simulatedHandover = 'Handed to accounting (simulated)'

export function caseStageLabel(stage: CaseStage): string {
  return caseStageLabels[stage]
}
