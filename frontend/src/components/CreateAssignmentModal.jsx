import { createPortal } from 'react-dom'
import { useState } from 'react'
import DropdownSelect from './DropdownSelect'

export default function CreateAssignmentModal({
  children,
  lessons,
  onSave,
  onClose,
  isSaving,
  error,
  preselectedLessonId = '',
}) {
  const [form, setForm] = useState({ childId: '', lessonId: preselectedLessonId, dueDate: '' })

  const childOptions = children.map((c) => ({
    value: c.id,
    label: `${c.name} · Grade ${c.grade}`,
  }))

  const lessonOptions = lessons.map((l) => ({
    value: l.id,
    label: l.title,
    description: `${l.subject} · Grade ${l.grade} · ${l.topic}`,
  }))

  function handleSubmit(e) {
    e.preventDefault()
    if (!form.childId || !form.lessonId) return
    onSave({
      childId: form.childId,
      lessonId: form.lessonId,
      dueDate: form.dueDate ? new Date(form.dueDate).toISOString() : null,
    })
  }

  return createPortal(
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-card" style={{ maxWidth: 520 }} onClick={(e) => e.stopPropagation()}>
        <div className="modal-header" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '1.25rem' }}>
          <h3 style={{ margin: 0 }}>Create assignment</h3>
          <button type="button" className="modal-close-btn" onClick={onClose}>✕</button>
        </div>
        <form onSubmit={handleSubmit}>
          <DropdownSelect
            id="modal-child"
            label="Child"
            placeholder="Select child"
            value={form.childId}
            options={childOptions}
            onChange={(v) => setForm((f) => ({ ...f, childId: v }))}
            disabled={children.length === 0}
            searchable
            searchPlaceholder="Search child"
            size="compact"
          />
          <div style={{ marginTop: '0.75rem' }}>
            <DropdownSelect
              id="modal-lesson"
              label="Lesson"
              placeholder="Select lesson"
              value={form.lessonId}
              options={lessonOptions}
              onChange={(v) => setForm((f) => ({ ...f, lessonId: v }))}
              disabled={lessons.length === 0}
              searchable
              searchPlaceholder="Search lesson"
              size="compact"
            />
          </div>
          <div className="field" style={{ marginTop: '0.75rem' }}>
            <label htmlFor="modal-due-date">
              Due date <span style={{ opacity: 0.45, fontSize: '0.8em' }}>(optional)</span>
            </label>
            <input
              id="modal-due-date"
              className="input"
              type="datetime-local"
              value={form.dueDate}
              onChange={(e) => setForm((f) => ({ ...f, dueDate: e.target.value }))}
            />
          </div>
          {error ? <div className="alert" style={{ marginTop: '0.75rem' }}>{error}</div> : null}
          <div className="button-row modal-actions" style={{ marginTop: '1.25rem' }}>
            <button
              type="submit"
              className="button"
              disabled={isSaving || !form.childId || !form.lessonId}
            >
              {isSaving ? 'Creating...' : 'Create assignment'}
            </button>
            <button type="button" className="button-secondary" onClick={onClose}>Cancel</button>
          </div>
        </form>
      </div>
    </div>,
    document.body,
  )
}
