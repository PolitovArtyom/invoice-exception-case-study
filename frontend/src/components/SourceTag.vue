<script setup lang="ts">
defineProps<{ source: string }>()

const labels: Record<string, string> = {
  invoice: 'Invoice',
  company_data: 'Company data',
  order: 'Order',
  receipts: 'Receipts',
  history: 'History',
  calculated: 'Calculated',
  policy: 'Policy',
}
</script>

<template>
  <span :class="['source-tag', `source-${source}`]">{{
    labels[source] ?? source
  }}</span>
</template>
