<script setup lang="ts">
import { ref } from 'vue'
import Button from 'primevue/button'
import {
  ActionError,
  correctFact,
  staleHumanActionMessage,
  type CorrectableFact,
  type MissingReason,
} from '../api'

const props = defineProps<{
  caseId: string
  revision: number
  field: CorrectableFact
  label: string
  previousValue: string | null
}>()
const emit = defineEmits<{ refresh: [message: string]; cancel: [] }>()
const value = ref(props.previousValue ?? '')
const missing = ref(props.previousValue === null)
const missingReason = ref<MissingReason | ''>('')
const person = ref('')
const reason = ref('')
const busy = ref(false)
const error = ref('')

async function submit() {
  if (missing.value && !missingReason.value) return
  busy.value = true
  error.value = ''
  try {
    const { result } = await correctFact(props.caseId, {
      expected_revision: props.revision,
      field_path: props.field.path,
      value: missing.value ? null : value.value,
      missing_reason: missing.value ? missingReason.value || null : null,
      person: person.value,
      reason: reason.value,
    })
    emit('refresh', `Correction saved in revision ${result.input_revision}.`)
  } catch (failure) {
    if (
      failure instanceof ActionError &&
      failure.field === '/expected_revision'
    )
      emit('refresh', staleHumanActionMessage)
    else error.value = (failure as Error).message
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <form
    class="human-action-form fact-correction"
    :aria-label="`Correct ${label}`"
    @submit.prevent="submit"
  >
    <label v-if="field.can_be_missing" class="missing-toggle">
      <input v-model="missing" type="checkbox" />
      Mark value as missing
    </label>
    <label v-if="missing">
      Why the value is missing
      <select v-model="missingReason" required>
        <option value="" disabled>Select reason</option>
        <option value="not_present_in_document">Not printed in the PDF</option>
        <option value="illegible">Illegible in the PDF</option>
      </select>
    </label>
    <label v-else>
      New value
      <input v-model="value" required />
    </label>
    <label>
      Your name
      <input v-model="person" required maxlength="128" autocomplete="name" />
    </label>
    <label>
      Reason for correction
      <textarea v-model="reason" rows="2" required />
    </label>
    <div class="human-action-buttons">
      <Button
        type="submit"
        label="Save correction"
        :loading="busy"
        :disabled="!person.trim() || !reason.trim()"
      />
      <Button
        type="button"
        label="Cancel"
        severity="secondary"
        :disabled="busy"
        @click="emit('cancel')"
      />
    </div>
    <p v-if="error" role="alert">{{ error }}</p>
  </form>
</template>
