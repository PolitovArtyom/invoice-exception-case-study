<script setup lang="ts">
import type { InvoiceCheck } from '../api'
import { checkTitle, verdictLabel } from '../checkPresentation'

defineProps<{ check: InvoiceCheck }>()
</script>

<template>
  <span :class="['check-verdict', 'verdict-' + check.status]">{{
    verdictLabel(check)
  }}</span>
  <span v-if="check.depends_on" class="muted">
    Waiting for {{ checkTitle(check.depends_on).toLowerCase() }}
  </span>
  <span v-else-if="check.status === 'not_applicable'" class="muted">
    Invoice and order units differ
  </span>
</template>
