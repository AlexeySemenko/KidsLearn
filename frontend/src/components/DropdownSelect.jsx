import { useEffect, useRef, useState } from 'react'

export default function DropdownSelect({
  id,
  label,
  placeholder,
  value,
  options,
  onChange,
  disabled = false,
  helperText,
  emptyMessage = 'No options available yet.',
  noResultsMessage = 'No matching options found.',
  size = 'compact',
  searchable = false,
  searchPlaceholder = 'Search...',
}) {
  const [isOpen, setIsOpen] = useState(false)
  const [query, setQuery] = useState('')
  const [activeIndex, setActiveIndex] = useState(-1)
  const containerRef = useRef(null)
  const triggerRef = useRef(null)
  const searchInputRef = useRef(null)
  const optionRefs = useRef([])

  const selectedOption = options.find((option) => option.value === value) ?? null
  const triggerSizeClass = size === 'compact' ? 'select-trigger-compact' : ''
  const optionSizeClass = size === 'compact' ? 'select-option-compact' : ''
  const normalizedQuery = query.trim().toLowerCase()
  const filteredOptions = !normalizedQuery
    ? options
    : options.filter((option) => {
      const label = option.label?.toLowerCase() ?? ''
      const description = option.description?.toLowerCase() ?? ''
      return label.includes(normalizedQuery) || description.includes(normalizedQuery)
    })

  useEffect(() => {
    if (!isOpen) {
      setQuery('')
      setActiveIndex(-1)
      return
    }

    if (searchable) {
      searchInputRef.current?.focus()
    }
  }, [isOpen, searchable])

  useEffect(() => {
    if (!isOpen) {
      return
    }

    const selectedIndex = filteredOptions.findIndex((option) => option.value === value)
    if (selectedIndex >= 0) {
      setActiveIndex(selectedIndex)
      return
    }

    setActiveIndex(filteredOptions.length > 0 ? 0 : -1)
  }, [filteredOptions, isOpen, value])

  useEffect(() => {
    if (!isOpen || activeIndex < 0) {
      return
    }

    optionRefs.current[activeIndex]?.scrollIntoView({ block: 'nearest' })
  }, [activeIndex, isOpen])

  useEffect(() => {
    if (!isOpen) {
      return undefined
    }

    function handlePointerDown(event) {
      if (!containerRef.current?.contains(event.target)) {
        setIsOpen(false)
      }
    }

    function handleEscape(event) {
      if (event.key === 'Escape') {
        setIsOpen(false)
      }
    }

    document.addEventListener('mousedown', handlePointerDown)
    document.addEventListener('keydown', handleEscape)

    return () => {
      document.removeEventListener('mousedown', handlePointerDown)
      document.removeEventListener('keydown', handleEscape)
    }
  }, [isOpen])

  function handleSelect(nextValue) {
    onChange(nextValue)
    setIsOpen(false)
    triggerRef.current?.focus()
  }

  function moveActiveIndex(step) {
    if (!filteredOptions.length) {
      return
    }

    if (activeIndex < 0) {
      setActiveIndex(step > 0 ? 0 : filteredOptions.length - 1)
      return
    }

    const nextIndex = (activeIndex + step + filteredOptions.length) % filteredOptions.length
    setActiveIndex(nextIndex)
  }

  function handleKeyDown(event) {
    if (disabled) {
      return
    }

    if (event.key === 'Tab' && isOpen) {
      setIsOpen(false)
      return
    }

    if (event.key === 'Escape' && isOpen) {
      event.preventDefault()
      setIsOpen(false)
      triggerRef.current?.focus()
      return
    }

    if (event.key === 'Home' && isOpen) {
      event.preventDefault()
      if (filteredOptions.length > 0) {
        setActiveIndex(0)
      }
      return
    }

    if (event.key === 'End' && isOpen) {
      event.preventDefault()
      if (filteredOptions.length > 0) {
        setActiveIndex(filteredOptions.length - 1)
      }
      return
    }

    if (event.key === 'ArrowDown') {
      event.preventDefault()
      if (!isOpen) {
        setIsOpen(true)
        return
      }

      moveActiveIndex(1)
      return
    }

    if (event.key === 'ArrowUp') {
      event.preventDefault()
      if (!isOpen) {
        setIsOpen(true)
        return
      }

      moveActiveIndex(-1)
      return
    }

    if (event.key === 'Enter' && isOpen && activeIndex >= 0) {
      event.preventDefault()
      handleSelect(filteredOptions[activeIndex].value)
    }
  }

  function getEmptyStateMessage() {
    if (options.length === 0) {
      return emptyMessage
    }

    return noResultsMessage
  }

  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      <div className={`select-shell${isOpen ? ' open' : ''}${disabled ? ' disabled' : ''}`} ref={containerRef}>
        <button
          ref={triggerRef}
          id={id}
          type="button"
          className={`select-trigger ${triggerSizeClass}${selectedOption ? '' : ' placeholder'}`}
          onClick={() => {
            if (!disabled) {
              setIsOpen((current) => !current)
            }
          }}
          aria-haspopup="listbox"
          aria-expanded={isOpen}
          aria-controls={`${id}-listbox`}
          disabled={disabled}
          onKeyDown={handleKeyDown}
        >
          <span className="select-trigger-copy">
            <span className="select-trigger-label">{selectedOption?.label ?? placeholder}</span>
            <span className="select-trigger-description">{selectedOption?.description ?? helperText}</span>
          </span>
          <span className="select-trigger-icon" aria-hidden="true">⌄</span>
        </button>

        {isOpen ? (
          <div className="select-popover" role="listbox" aria-labelledby={id} id={`${id}-listbox`}>
            {searchable ? (
              <div className="select-search-shell">
                <input
                  ref={searchInputRef}
                  type="text"
                  className="input select-search-input"
                  placeholder={searchPlaceholder}
                  value={query}
                  onChange={(event) => setQuery(event.target.value)}
                  onKeyDown={handleKeyDown}
                />
              </div>
            ) : null}

            {filteredOptions.length > 0 ? (
              filteredOptions.map((option, optionIndex) => (
                <button
                  key={option.value}
                  type="button"
                  className={`select-option ${optionSizeClass}${option.value === value ? ' selected' : ''}${filteredOptions[activeIndex]?.value === option.value ? ' focused' : ''}`}
                  onClick={() => handleSelect(option.value)}
                  onMouseEnter={() => setActiveIndex(optionIndex)}
                  role="option"
                  aria-selected={option.value === value}
                  id={`${id}-option-${option.value}`}
                  ref={(element) => {
                    optionRefs.current[optionIndex] = element
                  }}
                >
                  <span className="select-option-label">{option.label}</span>
                  <span className="select-option-description">{option.description}</span>
                </button>
              ))
            ) : (
              <div className="select-empty">{getEmptyStateMessage()}</div>
            )}
          </div>
        ) : null}
      </div>
      <span className="field-hint">{helperText}</span>
    </div>
  )
}
