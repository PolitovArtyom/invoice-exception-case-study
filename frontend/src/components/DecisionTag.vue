<script setup lang="ts">
import { computed } from 'vue'
import Tag from 'primevue/tag'
import type { CaseStage } from '../caseStage'

const props = defineProps<{ decision: string | null; stage?: CaseStage }>()
const outcome = computed(() =>
  props.stage === 'closed' ? 'duplicate' : props.decision,
)

const decisions: Record<string, string> = {
  clear: 'Clear',
  needs_review: 'Needs review',
  needs_information: 'Needs information',
  blocked: 'Blocked',
  duplicate: 'Duplicate',
}
</script>

<template>
  <Tag
    v-if="outcome"
    rounded
    :severity="
      outcome === 'clear'
        ? 'success'
        : outcome === 'duplicate'
          ? 'secondary'
          : 'warn'
    "
  >
    <span class="decision-dot" aria-hidden="true"></span
    >{{ decisions[outcome] ?? outcome }}
  </Tag>
  <Tag v-else rounded severity="secondary" value="Awaiting invoice reading" />
</template>
