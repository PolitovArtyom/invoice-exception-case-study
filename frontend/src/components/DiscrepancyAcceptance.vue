<script setup lang="ts">
import { ref } from 'vue'
import Button from 'primevue/button'
import {
  ActionError,
  recordDiscrepancyAcceptance,
  staleHumanActionMessage,
  type DiscrepancyAcceptance,
} from '../api'

const props = defineProps<{
  caseId: string
  revision: number
  assessmentId: string | null
  acceptance: DiscrepancyAcceptance | null
}>()
const emit = defineEmits<{ refresh: [message: string] }>()
const open = ref(false)
const busy = ref(false)
const person = ref('')
const reason = ref('')
const error = ref('')

async function submit() {
  if (!props.assessmentId) return
  busy.value = true
  error.value = ''
  try {
    await recordDiscrepancyAcceptance(props.caseId, {
      assessment_id: props.assessmentId,
      expected_revision: props.revision,
      person: person.value,
      reason: reason.value,
    })
    emit(
      'refresh',
      'Discrepancy accepted. The saved rules result stays Needs review.',
    )
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
  <section
    v-if="acceptance"
    class="card"
    aria-label="Recorded discrepancy acceptance"
  >
    <strong>Accepted with discrepancy by {{ acceptance.person }}</strong>
    <p>
      <q>{{ acceptance.reason }}</q>
    </p>
    <p class="muted">
      <RouterLink
        v-if="acceptance.assessment_id !== assessmentId"
        :to="{
          name: 'revision',
          params: { caseId, revision: acceptance.assessment_id },
        }"
      >
        Assessment {{ acceptance.assessment_id }}
      </RouterLink>
      <template v-else>Assessment {{ acceptance.assessment_id }}</template>
      ·
      <time :datetime="acceptance.recorded_at">{{
        new Date(acceptance.recorded_at).toLocaleString()
      }}</time>
    </p>
  </section>
  <section v-else class="card" aria-label="Discrepancy acceptance">
    <Button
      v-if="!open"
      type="button"
      label="Accept discrepancy"
      severity="secondary"
      @click="open = true"
    />
    <form v-else class="human-action-form" @submit.prevent="submit">
      <h2>Accept discrepancy</h2>
      <p>
        The case moves to Ready for accounting. The rules still say Needs
        review.
      </p>
      <label>
        Your name
        <input v-model="person" required maxlength="128" autocomplete="name" />
      </label>
      <label>
        Reason for acceptance
        <textarea v-model="reason" rows="2" required />
      </label>
      <div class="human-action-buttons">
        <Button
          type="submit"
          label="Save acceptance"
          :loading="busy"
          :disabled="!person.trim() || !reason.trim()"
        />
        <Button
          type="button"
          label="Cancel"
          severity="secondary"
          :disabled="busy"
          @click="open = false"
        />
      </div>
      <p v-if="error" role="alert">{{ error }}</p>
    </form>
  </section>
</template>
