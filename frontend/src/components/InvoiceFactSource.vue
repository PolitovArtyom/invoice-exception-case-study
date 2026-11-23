<script setup lang="ts">
import type { CorrectableFact, InvoiceEvidence } from '../api'
import FactCorrectionForm from './FactCorrectionForm.vue'

defineProps<{
  caseId: string
  revision: number
  label: string
  value: string | null
  quotes?: InvoiceEvidence[]
  field?: CorrectableFact
  editing: boolean
}>()
const emit = defineEmits<{
  edit: []
  cancel: []
  refresh: [message: string]
}>()
</script>

<template>
  <ul v-if="quotes?.length" class="fact-values">
    <li v-for="(quote, index) in quotes" :key="index">
      <q>{{ quote.excerpt }}</q>
      <span class="muted">Page {{ quote.page }}</span>
    </li>
  </ul>
  <span v-else class="muted">—</span>
  <template v-if="field">
    <FactCorrectionForm
      v-if="editing"
      :field="field"
      :label="label"
      :case-id="caseId"
      :revision="revision"
      :previous-value="value"
      @refresh="emit('refresh', $event)"
      @cancel="emit('cancel')"
    />
    <button
      v-else
      type="button"
      class="fact-edit-button"
      :aria-label="`Correct ${label}`"
      @click="emit('edit')"
    >
      Correct value
    </button>
  </template>
</template>
